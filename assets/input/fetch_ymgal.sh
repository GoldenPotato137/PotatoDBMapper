#!/usr/bin/env bash

cd "$(dirname "$0")" || exit
mkdir ymgal
cd ymgal || exit

DOWNLOAD_URL="https://github.com/ymgal/data-export/releases/latest/download/exported_data.zip"

echo "Downloading from $DOWNLOAD_URL ..."
wget "$DOWNLOAD_URL" -O ymgal.zip

unzip ymgal.zip
mv game.jsonl ../ymgal_game.jsonl

cd .. || exit
rm -r ymgal
