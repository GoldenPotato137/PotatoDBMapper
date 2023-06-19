wget -O vndb.tar.zst https://dl.vndb.org/dump/vndb-db-latest.tar.zst
zstd -d vndb.tar.zst
tar -xvf vndb.tar
mv db/vn* .