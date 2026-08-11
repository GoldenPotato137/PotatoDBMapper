using System.Collections.Concurrent;
using System.Text;

namespace PotatoDBMapper.Utils;

/// <summary>
/// 一次匹配的结果
/// </summary>
/// <param name="Id">命中的名字所属的id</param>
/// <param name="Distance">归一化后两个名字的编辑距离</param>
/// <param name="Similarity">相似度，<c>1 - Distance / max(两个名字的长度)</c></param>
public readonly record struct NameMatch(int Id, int Distance, float Similarity);

/// <summary>
/// 游戏名的索引，用来快速地按名字找出最相似的游戏。<br/>
/// 直接两两算编辑距离的话，n个名字配m个名字要算n*m次，几万配几万就是上百亿次，实在跑不动。所以这里：
/// <list type="number">
/// <item>先拿归一化后的名字查字典做精确匹配，绝大部分名字都能在这一步命中；</item>
/// <item>没命中的用二元组倒排索引筛候选。编辑距离为d的两个字符串至少有 max(len)-1-2d
/// 个公共二元组，达不到这个数量的候选连算编辑距离的必要都没有；</item>
/// <item>剩下的少量候选才算编辑距离，而且只填dp矩阵主对角线附近的带，一超过上界就退出。</item>
/// </list>
/// </summary>
public sealed class NameIndex
{
    /// <summary>归一化后的名字</summary>
    private readonly string[] _names;

    /// <summary>与<see cref="_names"/>一一对应的id</summary>
    private readonly int[] _ids;

    /// <summary>归一化后的名字 -> <see cref="_names"/>的下标，用于精确匹配</summary>
    private readonly Dictionary<string, List<int>> _exact;

    /// <summary>二元组 -> 它的倒排链在<see cref="_postings"/>中的位置</summary>
    private readonly Dictionary<int, int> _bigramSlots;

    /// <summary>第slot条倒排链在<see cref="_postings"/>中的区间为[start[slot], start[slot + 1])</summary>
    private readonly int[] _postingStarts;

    /// <summary>所有倒排链拼接而成的大数组，内容为<see cref="_names"/>的下标</summary>
    private readonly int[] _postings;

    private readonly ConcurrentBag<Matcher> _pool = new();

    public int Count => _names.Length;

    /// <summary>归一化后最长的名字有多长</summary>
    public int MaxLength { get; }

    public NameIndex(IEnumerable<(string name, int id)> names)
    {
        List<string> normalized = new();
        List<int> ids = new();
        foreach (var (name, id) in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var value = Normalize(name);
            if (value.Length == 0) continue;
            normalized.Add(value);
            ids.Add(id);
        }

        _names = normalized.ToArray();
        _ids = ids.ToArray();

        _exact = new Dictionary<string, List<int>>(_names.Length);
        for (var i = 0; i < _names.Length; i++)
        {
            MaxLength = Math.Max(MaxLength, _names[i].Length);
            if (_exact.TryGetValue(_names[i], out var same)) same.Add(i);
            else _exact[_names[i]] = [i];
        }

        // 建二元组倒排索引。扫两遍把所有倒排链拼进一个大数组里，比每条链一个List省内存也更快
        var counts = new Dictionary<int, int>();
        var seen = new HashSet<int>();
        foreach (var name in _names)
        {
            seen.Clear();
            for (var i = 0; i + 1 < name.Length; i++)
            {
                var key = BigramKey(name, i);
                if (seen.Add(key) == false) continue; // 同一个名字里重复出现的二元组只记一次
                counts.TryGetValue(key, out var count);
                counts[key] = count + 1;
            }
        }

        _bigramSlots = new Dictionary<int, int>(counts.Count);
        _postingStarts = new int[counts.Count + 1];
        int slot = 0, total = 0;
        foreach (var (key, count) in counts)
        {
            _bigramSlots[key] = slot;
            _postingStarts[slot] = total;
            total += count;
            slot++;
        }

        _postingStarts[slot] = total;
        _postings = new int[total];
        var cursors = (int[])_postingStarts.Clone();
        for (var i = 0; i < _names.Length; i++)
        {
            var name = _names[i];
            seen.Clear();
            for (var j = 0; j + 1 < name.Length; j++)
            {
                var key = BigramKey(name, j);
                if (seen.Add(key) == false) continue;
                _postings[cursors[_bigramSlots[key]]++] = i;
            }
        }
    }

    /// <summary>
    /// 索引里有没有这个名字（要求已经<see cref="Normalize"/>过）
    /// </summary>
    public bool Contains(string normalizedName) => _exact.ContainsKey(normalizedName);

    /// <summary>
    /// 新建一个匹配器。单个匹配器不是线程安全的，多线程时请每个线程各建一个
    /// </summary>
    public Matcher CreateMatcher() => new(this);

    /// <summary>
    /// 找出与给定名字最相似的那个id，没有相似度达到threshold的则返回null。<br/>
    /// 这个方法是线程安全的，每次调用会从池里租一个匹配器
    /// </summary>
    public NameMatch? MatchBest(string name, float threshold)
    {
        if (_pool.TryTake(out var matcher) == false) matcher = CreateMatcher();
        try
        {
            return matcher.MatchBest(name, threshold);
        }
        finally
        {
            _pool.Add(matcher);
        }
    }

    /// <summary>
    /// 归一化游戏名：全角转半角、统一大小写、去掉空白，并统一常见的波浪号与连字符写法
    /// </summary>
    public static string Normalize(string name)
    {
        try
        {
            name = name.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            // 少数脏数据可能不是合法的unicode，直接用原名
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c)) continue;
            builder.Append(c switch
            {
                '〜' or '～' or '∼' or '⁓' => '~',
                '‐' or '‑' or '‒' or '–' or '—' or '―' or '−' => '-',
                _ => c
            });
        }

        return builder.ToString();
    }

    private static int BigramKey(string name, int index) => (name[index] << 16) | name[index + 1];

    /// <summary>
    /// 匹配器。内部缓存了匹配过程中用到的数组以避免反复分配，因此<b>不是线程安全的</b>
    /// </summary>
    public sealed class Matcher
    {
        private readonly NameIndex _index;

        /// <summary>候选名字与当前查询的公共二元组数量</summary>
        private readonly int[] _common;

        /// <summary><see cref="_common"/>中被改过的下标，用于快速清零</summary>
        private readonly List<int> _touched = [];

        private readonly int[] _dpPrevious;
        private readonly int[] _dpCurrent;
        private readonly List<NameMatch> _result = [];

        internal Matcher(NameIndex index)
        {
            _index = index;
            _common = new int[index.Count];
            _dpPrevious = new int[index.MaxLength + 2];
            _dpCurrent = new int[index.MaxLength + 2];
        }

        /// <summary>
        /// 找出所有相似度不低于threshold的名字。名字能精确匹配上时只返回精确匹配的结果。<br/>
        /// 注意返回的列表会在下次调用时被复用，需要留着的话请自己拷一份
        /// </summary>
        public IReadOnlyList<NameMatch> Match(string name, float threshold)
        {
            _result.Clear();
            var query = Normalize(name);
            if (query.Length == 0) return _result;
            if (_index._exact.TryGetValue(query, out var hits))
            {
                foreach (var hit in hits)
                    _result.Add(new NameMatch(_index._ids[hit], 0, 1f));
                return _result;
            }

            MatchFuzzily(query, threshold);
            return _result;
        }

        /// <summary>
        /// 找出与给定名字最相似的那个id，没有相似度达到threshold的则返回null
        /// </summary>
        public NameMatch? MatchBest(string name, float threshold)
        {
            NameMatch? best = null;
            foreach (var match in Match(name, threshold))
            {
                // 相似度相同时取id较小的，好让多次运行的结果保持一致
                if (best is { } exist && (match.Similarity < exist.Similarity ||
                                          (match.Similarity == exist.Similarity && match.Id >= exist.Id))) continue;
                best = match;
            }

            return best;
        }

        private void MatchFuzzily(string query, float threshold)
        {
            for (var i = 0; i + 1 < query.Length; i++)
            {
                if (_index._bigramSlots.TryGetValue(BigramKey(query, i), out var slot) == false) continue;
                for (var p = _index._postingStarts[slot]; p < _index._postingStarts[slot + 1]; p++)
                {
                    var candidate = _index._postings[p];
                    if (_common[candidate]++ == 0) _touched.Add(candidate);
                }
            }

            foreach (var candidate in _touched)
            {
                var common = _common[candidate];
                _common[candidate] = 0;
                var name = _index._names[candidate];
                var maxLength = Math.Max(query.Length, name.Length);
                // similarity = 1 - distance / max(len)，所以相似度达到阈值等价于编辑距离不超过maxDistance
                var maxDistance = (int)((1 - threshold) * maxLength);
                // 计数过滤：编辑距离为d的两个字符串至少有 max(len) - 1 - 2d 个公共二元组
                if (common < maxLength - 1 - 2 * maxDistance) continue;
                var distance = BoundedLevenshtein(query, name, maxDistance);
                if (distance < 0) continue;
                _result.Add(new NameMatch(_index._ids[candidate], distance, 1 - distance / (float)maxLength));
            }

            _touched.Clear();
        }

        /// <summary>
        /// 计算两个字符串的编辑距离，只填dp矩阵中主对角线附近宽为maxDistance的带（Ukkonen）<br/>
        /// 距离超过maxDistance时直接返回-1，不必算出准确值
        /// </summary>
        private int BoundedLevenshtein(string s1, string s2, int maxDistance)
        {
            int n = s1.Length, m = s2.Length;
            if (Math.Abs(n - m) > maxDistance) return -1;

            // 编辑距离超过maxDistance的路径不必区分具体值，一律当成无穷大
            var infinity = maxDistance + 1;
            int[] previous = _dpPrevious, current = _dpCurrent;
            for (var j = 0; j <= m; j++)
                previous[j] = j;

            for (var i = 1; i <= n; i++)
            {
                int from = Math.Max(1, i - maxDistance), to = Math.Min(m, i + maxDistance);
                current[from - 1] = from == 1 ? i : infinity;
                if (to < m) current[to + 1] = infinity; // 带外的格子会被下一行读到
                var rowMin = int.MaxValue;
                for (var j = from; j <= to; j++)
                {
                    var value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1),
                        previous[j - 1] + (s1[i - 1] == s2[j - 1] ? 0 : 1));
                    current[j] = value;
                    if (value < rowMin) rowMin = value;
                }

                if (rowMin > maxDistance) return -1; // 整行都超了，后面不可能再降回来
                (previous, current) = (current, previous);
            }

            var distance = previous[m];
            return distance > maxDistance ? -1 : distance;
        }
    }
}
