stemCAT 2 - a small desk buddy that lives on your desktop
=========================================================

START
  Run build.bat to build and start stemCAT, or double-click stemCAT.exe after building.
  The cat appears in the bottom-right corner of your
  screen and a cat-face icon appears in the system tray (near the clock).
  If Windows SmartScreen warns about an unknown app, click "More info" > "Run anyway"
  (it's unsigned because you built it yourself), or run build.bat, which compiles a
  fresh stemCAT.exe from the source in the src folder with the C# compiler that is
  already part of Windows.

CHARACTERS (right-click > Character)
  Cat (15 coats), Fox, Bunny, Panda, Alien, Robot, Hacker (glowing eyes in a hoodie,
  green code bits fly up as you type), Cyber Cat (neon + glitches) and Ghost.
  Additional characters: Night Cat, Wraith Hacker, and Cyber Stalker use taller,
  angular silhouettes. The original Cat, Hacker, and Cyber Cat remain available.
  Each has its own colour sets (right-click > Colours) and desk colours, and you can
  mix and save your own colours in Settings.

WHAT IT DOES
  - Typing: it presses the matching key with the correct paw, and that key lights up.
  - Key pop-ups: a little keycap with the key's name pops up for each key you press
    (turn off: right-click > Key pop-ups; always hidden in privacy mode).
  - The keyboard faces the character, like a real desk seen from the front (its space
    bar is nearest to it). Prefer it the other way? Untick "Keyboard faces the character".
  - Mouse: when you use your mouse it holds its own mouse, which moves, clicks and
    scrolls along with yours.
  - Eyes follow your pointer; the pupils widen when the pointer gets close.
  - After ~12 s idle it daydreams (sits up and gazes ahead); after ~45 s it falls
    asleep: eyes closed, tail on the desk, floating Zs.
  - Touch the keyboard while it sleeps and it shakes its head awake.
  - Press and hold its head to pet it: it squints, leans into your hand, hearts.

CONTROLS
  Drag the cat ...................... move it anywhere (a press-and-hold on the head pets
                                      it; pull further to drag)
  Right-click > Position ............ jump to a corner / centre / next monitor, or lock it
  Right-click the cat or tray icon .. menu: Settings, Coat, Size, Opacity, Quit ...
  Double-click the tray icon ........ Settings
  Ctrl+Alt+L ........................ click-through lock on/off (clicks pass through
                                      the cat, handy over games). Works even when locked.
  Ctrl+Alt+P ........................ privacy mode: shows only left paw / right paw,
                                      never which key

SETTINGS
  15 coats (Orange Tabby, Midnight Black, Snow White, Ash Gray, Siamese, Calico,
  Tuxedo, Tortoiseshell, Russian Blue, Cream, Brown Tabby, Silver Tabby,
  Ginger & White, Chocolate, Lilac), your own colour mixes saved under your own
  names, keyboard / key / key-light / mouse colours, size (Mini to Huge), opacity,
  green-screen background for streaming (OBS chroma key), start with Windows.
  Settings are kept in %APPDATA%\stemCAT\settings.ini.
  Existing settings from the previous app name are loaded automatically.

PRIVACY
  stemCAT only remembers which keys are held down right now, in memory, so the cat can
  press them. Each key is forgotten the moment you release it. Nothing is logged,
  saved or sent anywhere, and stemCAT never connects to the internet.

REQUIREMENTS
  Windows 10 or 11 (uses the built-in .NET Framework 4.x - nothing to install).

UNINSTALL
  Quit from the menu, untick "Start with Windows" first if you turned it on, then delete
  this folder and %APPDATA%\stemCAT.

