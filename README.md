# Wrenchables

"I want to use the wrench on my things, it's that simple."

Simple transformation tweaks to vanilla objects, adds rotation interaction and specialized tuning tools.

## Features

- Tuning Wrench: swaps a vanilla crate with a rotatable crate variant and back, keeping its contents
- Rotate crates in fine steps using the Tuning Wrench
- Firewood and aged firewood are displayed as stackable piles inside wrenchable crates

## Crafting

Place a vanilla wrench into the crafting grid to get the Tuning Wrench of the same metal. Place the Tuning Wrench into the crafting grid to get the vanilla wrench back.

## Controls

- [Ctrl] + [Right-click] on a rotatable crate: rotate it
- [Ctrl] + [Shift] + [Right-click] on a crate: swap between vanilla and rotatable variant

## Configuration

The config file wrenchables-config.json is created on first start. Adjust values to your liking, the mod will clean up nonsensical values so you needn't worry about that.

- FinerStepsMultiplier: rotation step as a fraction of 90 degrees (default value is 0.25 = 22.5 degrees)
- AmountFullAllowedBlockRotations: full turns allowed before the crate tips onto its side (default value is 1).

## License

All Rights Reserved, see the LICENSE file for usage permissions.

---

# [Update-Changelog]:

### Version 1.0.0

- Initial release.

  #### Modder info

  - This mod uses a command called ".checkitem" - should this cause issues, please report error logs under Issues.

  #### Currently supported:

  - Vanilla Crate (+ (Aged)Firewood)

End of file.
