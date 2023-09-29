# Using Overmind

Overmind is a process manager. It reads a `Procfile` at the root which is a simple list of `[name]: [executable]` entries. Overmind will launch and manage all of the processes listed in the file, and allows you to start/stop/restart them.

Before doing any of this, you need to `brew install overmind`

To start the entire universe, run `script/all` or just `overmind start` in the repo root. You'll get a cool log that shows everything that's running

<img width="863" alt="image" src="https://user-images.githubusercontent.com/7574/213798669-ee3bf07e-6198-4027-bf52-6694f800314b.png">

Leave that terminal running and use another tab/window for your interactive commands. **Or**, you can run `script/all -D` / `overwind start -D` to "daemonize" and it'll just start overmind running in the background:

<img width="897" alt="image" src="https://user-images.githubusercontent.com/7574/213799078-8e61bb7c-e1b9-4d92-a604-0edcc3c897fd.png">

Want to launch everything but the web app (so you can launch it under the debugger)? Just add `-e` to exclude it:

```shell
# script/all -e web
```

Or, made a change to the web app and want to restart it? Run `overmind restart`:

```shell
# overmind restart web
```

Or just stop the web app so you can relaunch under the debugger? Run `overmind stop`:

```shell
# overmind stop web
```

Now here's where things get cool. In a new terminal window, with the rest of the app running, run `overmind connect` in the repo root. That will launch `tmux`, the Terminal Multiplexer, which gives you a separate tab to view the stdout for each process that's running. You do need to do [a quick crash-course on tmux](https://densitylabs.io/blog/a-tmux-crash-course-tips-and-tweaks) so you know how to get around. The most important shortcut to know would be `Ctrl-b [number]` (that's press `Ctrl-b`, release it, then press any number key), which allows you to swap windows:

<https://user-images.githubusercontent.com/7574/213807352-8113de1e-5ac8-4ec8-a189-67be36c1c49e.mov>

Another good shortcut to have is is `Ctrl-b d` (again, press `Ctrl-b`, release, press `d`) which 'detaches' your current session. That will return you to the command prompt while leaving the tmux session running.

For more, check out the [overmind](https://github.com/DarthSim/overmind) docs.
