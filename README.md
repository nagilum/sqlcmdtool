# SQL Command-Line Tool

Simple command-line tool to launch Microsoft SQL Management Studio based on connection string in local web.config file.

Usage: `sql` `[options]`

* `--open` Read local web.config file, attempt to open Microsoft SQL Management Studio and login.
* `--test-cs` Use the connection string: `TestConnectionString`. Defaults to `MainConnectionString`.
* `--cs <name>` Use the named connection string. Defaults to `MainConnectionString`.
* `--list` List all found connection strings.

# How It Works

1. First it scans all directories from the current one and deeper to find all `web.config` files. If there are more than one, the user can select the correct one.
2. Then is looks at its local cache to see if the location of the .exe for SSMS is there, if not if scans the common folders for it, and caches the result for next time. If there are more than one hits, the user can select the correct one.
3. It fires up the SSMS app and waits for the connection dialog to appear. When it does, it fill in the server hostname, username, and password, then presses the connect button. It is important that you don't change the active window while this is going on. Usually it takes between 5 and 10 seconds for SSMS to launch and login.

# How It Looks

![The console output while the app is running with the --open switch](https://raw.githubusercontent.com/nagilum/sqlcmdtool/main/assets/images/what-it-looks-like.png)

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
