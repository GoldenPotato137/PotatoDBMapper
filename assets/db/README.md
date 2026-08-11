# 结果库

`vn_mapper.db` 不再提交进仓库了——每周十几MB的二进制diff会让仓库无止境地变大——
改成每次跑完覆盖发布到 [db-latest](https://github.com/GoldenPotato137/PotatoDBMapper/releases/tag/db-latest)
这一个release里，地址是固定的：

```
https://github.com/GoldenPotato137/PotatoDBMapper/releases/download/db-latest/vn_mapper.db
```

本地手动运行时，结果也会写在这个目录下。表结构见[根目录的README](../../README.md)。
