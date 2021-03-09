# SQL Command-Line Tool

Simple command-line tool to launch Microsoft SQL Management Studio based on connection string in local web.config file.

Usage: `sql` `[options]`

* `--open` Read local web.config file, attempt to open Microsoft SQL Management Studio and login.
* `--test-cs` Use the connection string: TestConnectionString. Defaults to MainConnectionString.
* `--cs <name>` Use the named connection string. Defaults to MainConnectionString.
* `--list` List all found connection strings.

# How To Use

*Open SSMS with default connection string*

```bash
sql --open
```

*Open SSMS with test connection string*

```bash
sql --open --test-cs
```

*Open SSMS by specifying a connection string*

```bash
sql --open --cs MyAwesomeConnectionString
```

*List all the available connection strings*

```bash
sql --list
```

# Features

If more than one web.config is found when executing you will be presented with a list of all hits so you can select the correct one.

If more than one instance of the Microsoft SQL Management Studio executable (ssms.exe) is found you will be presented with a list of all hits so you can select the correct one.
