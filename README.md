# TN 3270 Emulator
## This is a work in progress. Currently has limited functionality and only works using basic telnet commands with a limited tn3270 buffer management
### Doesn't support modes other than 24*80, at the moment.

## Usage
```
Terminal terminal = new Terminal("mainframe.com", 23);
terminal.WaitForTextAsync("Hello World");
terminal.TrySetText(0, "UserBob");
terminal.TrySetText(1, "Pwd1234$");
terminal.Send(AidKey.Enter);
terminal.WaitForTextAsync("Welcome");
```
