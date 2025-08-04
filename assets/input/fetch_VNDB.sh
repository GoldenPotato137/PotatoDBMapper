cd "$(dirname "$0")" || exit
mkdir vndb
cd vndb || exit

wget -O vndb.tar.zst https://dl.vndb.org/dump/vndb-db-latest.tar.zst
zstd -d vndb.tar.zst
tar -xvf vndb.tar
mv db/vn* ..

cd .. || exit
rm -r vndb