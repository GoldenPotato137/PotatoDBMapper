cd "$(dirname "$0")" || exit
mkdir bangumi
cd bangumi || exit

filename=$(curl -s "https://github.com/bangumi/Archive/releases/tag/archive" | grep -oP "(?<=latest dump filename: <code>).*(?=</code>)")
wget "https://github.com/bangumi/Archive/releases/download/archive/$filename" -O bgm.zip
unzip bgm.zip
mv subject.jsonlines ..

cd .. || exit
rm -r bangumi
