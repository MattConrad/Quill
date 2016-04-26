# Quill

Quill is a tool for playing, and eventually sandbox testing, Ink stories.

It's not intended as a "serious" authoring environment. Rather it is a sandbox for experimenting and trying out simple things with Ink. If you are curious about Ink this is a way to try it out online.

Quill is written against ASP.NET Core 1 (or vNext, or ASP.NET 5 . . .), which mixes things up considerably from previous versions of ASP.NET. If this is new to you, check out http://docs.asp.net/en/latest/conceptual-overview/aspnet.html before you try to build. A further warning, Quill is specifically built against Core RC1, which is marked for obsolescence. Things will change a lot in RC2. Watch here for updates.

The .dll and .exe in /lib are compiled against .NET Core (not Mono and not "regular" .NET). As long as you're building Quill against .NET Core they should work. If you want to build against a different rutime, or just prefer to use your own binaries, you'll need to build these yourself.

(Building the Ink runtime and Inklecate against .NET Core are a little tricky. This is a good RC1 resource for setting up a project.json and working through the issues. http://blog.marcgravell.com/2015/11/the-road-to-dnx-part-1.html. When RC2 arrives, this will change.)

You will need to fix the path in /wrap/ink-engine-runtime/project.json to a local path. At least according to Stack Overflow, the wrap feature only works with an absolute path. Hey, we're on the edge here.
