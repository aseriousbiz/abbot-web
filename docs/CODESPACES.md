# Codespaces

GitHub Codespaces are awesome! Here are some tips for using them.

## TL;DR

1. `gh cs create` - Create a Codespace
2. `gh cs code` - Open VS Code to the CodeSpace
3 `script/attach-runner-codespace` - Run this in a separate tab in the `aseriousbiz/abbot` directory to allow the skill runner to call back to `Abbot.Web`.
4. `gh cs ssh` - to SSH into the Codespace
5. `script/server` - in the SSH session to start the runner.


## End to end demo

Here's an end-to-end demo of launching the Python Skill Runner in a Codespace, but you can also find the same information in the docs below:

https://drive.google.com/file/d/1t3NV8OLlR-9ljtIqgZplWoFaSoOPCwRj/view?usp=sharing

## Running Skill Runners in Codespaces

The non-.NET skill runners, in [abbot-py](http://github.com/aseriousbiz/abbot-py) and [abbot-js](http://github.com/aseriousbiz/abbot-js), are configured to easily run in codespaces.
To run these in a Codespace, install the [gh](https://github.com/cli/cli) CLI tool and run `gh cs create` to create a codespace:

https://user-images.githubusercontent.com/7574/145649927-f8e2097c-cb98-40c7-98de-17f2c377528f.mp4

We recommend the `4 core` machine spec.
It provides a good balance of enough power to develop in quickly, which being fairly cost-effective.

Once you have the runner open in a Codespace, you can launch it and it should set up port-forwards appropriately so that Abbot.Web can talk to the runner.

**However**, Skill Runners also need to be able to reach Abbot.Web.
Currently, we don't recommend running Abbot.Web in a Codespace, so you need to tunnel the port Abbot.Web runs on **in** to your Codespace.
We provide a script for exactly that!
Just run the `script/attach-runner-codespace` command, select the appropriate Codespace, and it will start a tunnel.
You can test that the tunnel is working by running `curl -k https://localhost:4979` in your Codespace (`-k` tells curl to ignore certificate trust errors).

https://user-images.githubusercontent.com/7574/145649918-76fea143-0366-4690-977c-deb891bd8769.mp4

Once your Codespace is up and running, you can use the `gh cs` commands to manage it:

1. `gh cs delete` to delete codespaces
2. `gh cs ssh` to SSH in to your codespace
3. `gh cs code` to launch VS Code for your Codespace.

## Running Abbot.Web, ProxyLink and the .NET Runner in a Codespace

TBD!
Right now, we strongly advise using JetBrains Rider to work on our .NET Projects and it doesn't have native Codespaces support (yet...).
If you want to use VSCode, Codespaces _should_ work with Abbot.Web though!

## Codespaces Tips and Tricks

### GPG Signing

On the [Codespaces Settings Page](https://github.com/settings/codespaces), you can configure GPG verification.
In any Codespace for a repo you've configured for GPG verification, you can sign your commits (using `git commit -S` or by setting the `commit.gpgsign` config setting to true).

### Copying files and SSH

You can use the `gh cs cp` command line tool to copy files between your machine and codespaces!
You can also drag and drop in and out of VS Code.

Don't like VS Code? Prefer `vim`? You can SSH directly to your Codespace using `gh cs ssh`!
