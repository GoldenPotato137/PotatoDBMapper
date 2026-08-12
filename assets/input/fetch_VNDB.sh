cd "$(dirname "$0")" || exit
mkdir vndb
cd vndb || exit

wget -O vndb.tar.zst https://dl.vndb.org/dump/vndb-db-latest.tar.zst
zstd -d vndb.tar.zst
tar -xvf vndb.tar
# vn*是名字，extlinks那几张表里有vndb自己维护的steam链接
mv db/vn* db/extlinks* db/releases_extlinks* db/releases_vn* ..

cd .. || exit
rm -r vndb