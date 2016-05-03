# Quill

Quill is a tool for playing and tinkering with [Ink](https://github.com/inkle/ink) stories.

It's not intended as a "serious" story authoring environment. Rather it is a sandbox for experimenting and trying out simple things with Ink. If you are curious about Ink this is a way to try it out online.

## Trying Quill Online

You can try out Quill at http://jeejah.xyz/quill/.

## Building Quill

Quill is written against ASP.NET Core 1 (or vNext, or ASP.NET 5 . . . there's a profusion of names here), which mixes things up considerably from previous versions of ASP.NET. If this is new to you, check out http://docs.asp.net/en/latest/conceptual-overview/aspnet.html before you try to build. A further warning, Quill is specifically built against Core RC1, which is marked for obsolescence. Things will change a lot in RC2.

The .dll and .exe in /lib are compiled against .NET Core (not Mono and not "regular" .NET). As long as you're building Quill against .NET Core they should work (cough cough). If you want to build against a different runtime, or just prefer to use your own binaries, you'll need to build these yourself.

(Building the Ink runtime and Inklecate against .NET Core are a little tricky. This is a good RC1 resource for setting up a project.json and working through the issues, but be prepared to apply some workaround hacks as well. http://blog.marcgravell.com/2015/11/the-road-to-dnx-part-1.html. Again, when RC2 arrives, at least some things here will change.)

You'll need to revise the WebAppRoot setting in appsettings.json. "/" is a plausible value. This is a substitute for the '~' root operator that you get in ASP.NET on IIS, but don't get with nginx/Kestrel. There's probably a better way to handle this, drop me a note if you know what it is.

You will need to fix the path in /wrap/ink-engine-runtime/project.json to a local path. At least according to Stack Overflow, the wrap feature only works with an absolute path. Hey, we're on the edge here.
