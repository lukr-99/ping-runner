# SQLite for the history

The history keeps every ping of every run, which reaches hundreds of thousands of rows for a day-long
run at a short interval, and it must survive a crash mid-run and restore from a backup safely. One JSON
file per run would have to be rewritten on every save and has no transactions, so Ping Runner uses a
single SQLite file through Microsoft.Data.Sqlite, with CodePrint's numbered, checksummed migrations.
The cost is a native library in the package (about 1.5 MB) and a schema that changes only through
new migration files.
