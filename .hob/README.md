# .hob

This directory contains hob project metadata intended to be shared with the repo:
project identity, public signing keys, user profiles, automations, and explicit
shares. Local/private runtime state lives in hob's local database and config
directories, not here.

Project switcher icons can be provided as `icon.svg` or `icon.png` in this
directory. Theme-specific variants can be provided as `icon-dark.svg` /
`icon-dark.png` and `icon-light.svg` / `icon-light.png`. For each active theme
mode, hob prefers the matching SVG, then matching PNG, then falls back to
`icon.svg` or `icon.png`.
