# SQL Tool

Simple command-line tool to launch SQL Management Studio based on connection string in local web.config file.

Usage: `sql` `[options]`

* `--open` Read local web.config file, attempt to open Microsoft SQL Management Studio and login.
* `--test-cs` Use the connection string: TestConnectionString. Defaults to MainConnectionString.
* `--cs <name>` Use the named connection string. Defaults to MainConnectionString.
* `--list` List all found connection strings.

Examples:

* `sql --open`
* `sql --open --cs NewConnection`
* `sql --list`
