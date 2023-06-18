This is an export of the VNDB.org database.
See https://vndb.org/d14 for more information.


Usage
=====

Each table is exported as a separate file in PostgreSQL's COPY format. This
format is pretty easy to parse - it's TSV with some escape sequences.

Alternatively, a script is provided to load the data into a PostgreSQL
database for easy querying. See import.sql for options and usage information.

Note that the included database schema is *not* compatible with the VNDB
source code, Importing the data into a compatible schema should be possible,
but this will require some additional scripting.


Privacy
=======

This database dump contains user-contributed information, including personal
visual novel lists and votes. Users have the option to have their data
excluded from this dump, but that option is obviously unable to retroactively
delete data from older dumps. If you republish any data contained in this
dump, please make sure to synchronise regularly and remove data that is not
present anymore.


License
=======

This database is made available under the Open Database License [ODbL].
Any rights in individual contents of the database are licensed under the
Database Contents License [DbCL].

With the following exceptions:

Wikidata information (db/wikidata) has been obtained from Wikidata and is
made available under the Creative Commons CC0 License [CC0].

Anime data (db/anime) is obtained from the AniDB.net UDP API and is licensed
under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0
[CC-BY-NC-SA].

Visual novel descriptions (db/vn, 'desc' column) and character descriptions
(db/chars, 'desc' column) are gathered from various online sources and may be
subject to separate license conditions.

[ODbL]: LICENSE-ODBL.txt; http://opendatacommons.org/licenses/odbl/1.0/
[DbCL]: LICENSE-DBCL.txt; http://opendatacommons.org/licenses/dbcl/1.0/
[CC0]: LICENSE-CC0.txt; https://creativecommons.org/publicdomain/zero/1.0/
[CC-BY-NC-SA]: LICENSE-CC-BY-NC-SA.txt; https://creativecommons.org/licenses/by-nc-sa/4.0/