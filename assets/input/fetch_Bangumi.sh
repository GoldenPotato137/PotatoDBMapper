filename=$(curl -s "https://github.com/bangumi/Archive/releases/tag/archive" | grep -oP "(?<=latest dump filename: <code>).*(?=</code>)")
wget "https://github.com/bangumi/Archive/releases/download/archive/$filename"
