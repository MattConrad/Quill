// @title(Cloak of Darkness)
// @byline(This implementation by Michael Akinde; original by Roger Firth)
=== opera_house
VAR scuffled = 0
VAR has_cloak = true
VAR hung_cloak = false
VAR dropped_cloak = false
VAR examined_self = false

Hurrying through the rainswept November night, you're glad to see the bright lights of the Opera House. It's surprising that there aren't more people about but, hey, what do you expect in a cheap demonstration story...?

* [Enter the foyer.] You enter the foyer of the Opera house.
  -> foyer

=== foyer
You are standing in a spacious hall, splendidly decorated in red and gold, with glittering chandeliers overhead. The entrance from the street is to the north, and there are doorways south and west.
-> foyer_options

== foyer_options
+ [Examine yourself.] You examine yourself.
    ~ examined_self = true
    { has_cloak : You are wearing a handsome cloak, of velvet trimmed with satin, and slightly splattered with raindrops. Its blackness is so deep that it almost seems to suck light from the room.|You aren't carrying anything. }
    -> foyer_options
+ Go north.
  -> leave
+ Go west.
  -> cloakroom
+ {not(has_cloak)} Go south.
  -> bar_light
+ {has_cloak} Go south.
  -> bar_dark

== leave
{No. You've only just arrived, and besides, the weather outside seems to be getting worse.|No. It's really raining cats and dogs out there.|Are you still considering that option? The answer is still no.|Come on, get on with the story.}
-> foyer_options


== cloakroom

The walls of this small room were clearly once lined with hooks, though now only one remains. The exit is a door to the east.

{dropped_cloak : Your cloak is on the floor here.}

{hung_cloak : Your cloak is hanging on the hook.}

-> cloakroom_options

== cloakroom_options
+ [Examine the hook.] You examine the hook.
  It's just a small brass hook{hung_cloak :, with your cloak hanging on it| screwed to the wall}.
  -> cloakroom_options
+ {has_cloak && examined_self} [Hang your cloak on the hook.]
  You hang your cloak on the hook.
  ~ has_cloak = false
  ~ hung_cloak = true
  -> cloakroom
+ {has_cloak && examined_self} [Drop your cloak on the floor.] You drop your cloak on the floor.
  ~ has_cloak = false
  ~ dropped_cloak = true
  -> cloakroom
+ {hung_cloak || dropped_cloak} [Pick up your cloak.]
  {hung_cloak : You take your cloak from the hook|You pick up your cloak from the floor}.
  ~ has_cloak = true
  ~ hung_cloak = false
  ~ dropped_cloak = false
  -> cloakroom
+ [Go east.] -> foyer


=== bar_dark
You walk to the bar, but it's so dark here you can't really make anything out. The foyer is back to the north.
-> bar_dark_options

== bar_dark_options
* [Feel around for a light switch.]
* [Sit on a bar stool.]
+ [Go north.] -> foyer
- In the dark? You could easily disturb something.
  ~ scuffled = scuffled + 1
  -> bar_dark_options


=== bar_light

The bar, much rougher than you'd have guessed after the opulence of the foyer to the north, is completely empty. There seems to be some sort of message scrawled in the sawdust on the floor. The foyer is back to the north.
+ [Examine the message.] -> message
+ [Go north.] -> foyer


== message
{
  - scuffled < 2 :
    The message, neatly marked in the sawdust, reads...
    You have won!
  - else :
    The message has been carelessly trampled, making it difficult to read. You can just distinguish the words...
    You have lost.
}

* The End.
  -> END