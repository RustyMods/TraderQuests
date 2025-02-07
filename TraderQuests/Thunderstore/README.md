# TraderQuest

Adds new UI panel to traders with Bounty, Treasure Hunt, Alt Shop, Gamble Slot Machine. Additionally, you can override vanilla trader shop items.

![](https://i.imgur.com/3nIQR0x.png)
![](https://i.imgur.com/rA30cWn.png)
![](https://i.imgur.com/OShMbKQ.png)
![](https://i.imgur.com/NJvz6W2.png)

# Configurations

- You can Enable/Disable tabs to your liking.
- You can define which Trader has access to this panel (Haldor, Hildir, Custom, All)
- Default Custom Trader uses Gravebear's TravelingTrader

# Bounty Configuration Guide

This guide will help you create and customize bounties for your game. Bounties define a set of creatures to be hunted, the rewards for completing the task, and additional parameters that control their availability.

## File Format

Bounties are defined in `.yml` files, following a structured format. Below is a breakdown of each component in the configuration.

---

## Example Bounty

```yml
UniqueID: NeckBounty.0001
Name: Nekken Bounty
Weight: 1
CurrencyItem: Coins
IconPrefab: TrophyNeck
Price: 10
Biome: Meadows
RequiredKey: defeated_eikthyr
Cooldown: 1000
Creatures:
  - UniqueID: NeckBoss
    PrefabName: Neck
    OverrideName: Nekken
    Level: 3
    IsBoss: true
  - UniqueID: NeckMinion.0001
    PrefabName: Neck
    OverrideName: Nekken Minion
    Level: 1
    IsBoss: false
  - UniqueID: NeckMinion.0002
    PrefabName: Neck
    OverrideName: Nekken Minion
    Level: 1
    IsBoss: false
Rewards:
  - PrefabName: Coins
    Amount: 50
    Quality: 0
  - PrefabName: TraderCoin_RS
    Amount: 1
    Quality: 0
EpicMMOExp: 0
AlmanacExp: 0
```

---

## Configuration Fields

### General Settings

- **UniqueID** *(string)* – A unique identifier for the bounty.
- **Name** *(string)* – Display name of the bounty.
- **Weight** *(float)* – Determines the likelihood of the bounty appearing in the panel.
- **CurrencyItem** *(string)* – The item required for bounty transactions.
- **IconPrefab** *(string)* – The icon used for displaying the bounty.
- **Price** *(int)* – Cost of accepting the bounty.
- **Biome** *(string)* – Specifies the biome where the bounty takes place.
- **RequiredKey** *(string)* – A prerequisite key required to access the bounty.
- **Cooldown** *(float)* – Time required before the bounty can be taken again.

### Creatures

Each bounty contains a list of creatures that must be hunted.

- **UniqueID** *(string)* – Unique identifier for the creature.
- **PrefabName** *(string)* – The game object representing the creature.
- **OverrideName** *(string)* – Display name for the creature.
- **Level** *(int)* – Creature's difficulty level.
- **IsBoss** *(bool)* – Determines if the creature is a boss.

### Rewards

Rewards define what the player earns upon completing the bounty.

- **PrefabName** *(string)* – The in-game item given as a reward.
- **Amount** *(int)* – Quantity of the item rewarded.
- **Quality** *(int)* – Quality level of the reward.

### Experience

- **EpicMMOExp** *(int)* – Experience points for the EpicMMO system.
- **AlmanacExp** *(int)* – Experience points for the Almanac system.

---

## Creating a New Bounty

1. Copy the example bounty and modify the fields according to your desired challenge.
2. Assign a unique `UniqueID`.
3. Define the creatures the player must hunt in the `Creatures` list.
4. Specify the rewards under `Rewards`.
5. Adjust `Cooldown`, `Price`, and other parameters as needed.
6. Save the file with a `.yml` extension and add it to your game's bounty directory.

---

## Tips

- Make sure each `UniqueID` is distinct to prevent conflicts.
- Use a balanced `Price` and `Rewards` ratio to ensure fair gameplay.
- Test your bounty in-game to confirm that all settings work as intended.

---

# Treasure Hunt Configuration Guide

This guide will help you create and customize treasure hunts for your game. Treasure hunts define hidden treasures, the rewards for discovering them, and additional parameters that control their availability.

## File Format
Treasure hunts are defined in `.yml` files, following a structured format. Below is a breakdown of each component in the configuration.

---

## Example Treasure Hunt
```yml
UniqueID: MeadowTreasure.0001
Name: The Forager’s Trove
Weight: 1
RequiredKey: ''
Biome: Meadows
IconPrefab: TraderMap_RS
CurrencyPrefab: Coins
Price: 10
Cooldown: 0
Rewards:
  - PrefabName: Wood
    Amount: 10
    Quality: 0
  - PrefabName: TraderCoin_RS
    Amount: 1
    Quality: 0
  - PrefabName: Club
    Amount: 1
    Quality: 3
```

---

## Configuration Fields

### General Settings
- **UniqueID** *(string)* – A unique identifier for the treasure hunt.
- **Name** *(string)* – Display name of the treasure hunt.
- **Weight** *(float)* – Determines the likelihood of the treasure appearing.
- **RequiredKey** *(string)* – A prerequisite key required to access the treasure.
- **Biome** *(string)* – Specifies the biome where the treasure is located.
- **IconPrefab** *(string)* – The icon used for displaying the treasure hunt.
- **CurrencyPrefab** *(string)* – The in-game currency required for acquiring the treasure.
- **Price** *(int)* – Cost of initiating the treasure hunt.
- **Cooldown** *(long)* – Time required before the treasure can be discovered again.

### Rewards
Rewards define what the player earns upon discovering the treasure.
- **PrefabName** *(string)* – The in-game item given as a reward.
- **Amount** *(int)* – Quantity of the item rewarded.
- **Quality** *(int)* – Quality level of the reward.

---

## Creating a New Treasure Hunt
1. Copy the example treasure hunt and modify the fields according to your desired challenge.
2. Assign a unique `UniqueID`.
3. Define the treasure location in the `Biome` field.
4. Specify the rewards under `Rewards`.
5. Adjust `Cooldown`, `Price`, and other parameters as needed.
6. Save the file with a `.yml` extension and add it to your game's treasure directory.

---

## Tips
- Make sure each `UniqueID` is distinct to prevent conflicts.
- Use a balanced `Price` and `Rewards` ratio to ensure fair gameplay.
- Test your treasure hunt in-game to confirm that all settings work as intended.

---

# Shop Configuration Guide

This guide will help you create and customize shop items for your game. The shop configuration defines items available for purchase, their cost, quality, and availability.

## File Format
Shop items are defined in `.yml` files, following a structured format. Below is a breakdown of each component in the configuration.

---

## Example Shop Item
```yml
PrefabName: SwordBronze
Stack: 1
Quality: 2
CurrencyPrefab: TraderCoin_RS
Price: 10
OnSalePrice: 0
RequiredKey: defeated_eikthyr
Weight: 1
CanBeOnSale: true
```

---

## Configuration Fields

### General Settings
- **PrefabName** *(string)* – The unique name of the item being sold.
- **Stack** *(int)* – The number of items sold per purchase.
- **Quality** *(int)* – The quality level of the item.
- **CurrencyPrefab** *(string)* – The currency required for purchasing the item.
- **Price** *(int)* – The base cost of the item.
- **OnSalePrice** *(int)* – The discounted price when the item is on sale.
- **RequiredKey** *(string)* – A prerequisite key required to unlock the item.
- **Weight** *(float)* – Determines the likelihood of the item appearing in the shop.
- **CanBeOnSale** *(bool)* – Specifies whether the item can go on sale.

---

## Creating a New Shop Item
1. Copy the example shop item and modify the fields according to your desired setup.
2. Assign a unique `PrefabName`.
3. Define the item’s `Quality`, `Price`, and `CurrencyPrefab`.
4. Adjust `OnSalePrice`, `RequiredKey`, and `CanBeOnSale` as needed.
5. Save the file with a `.yml` extension and add it to your game's shop directory.

---

## Tips
- Ensure each `PrefabName` is unique to prevent conflicts.
- Use `CanBeOnSale: true` only for items that should have discounts.
- Test your shop configuration in-game to confirm all settings function correctly.

---

# Trader Configuration Guide

This guide will help you configure in-game vanilla trader shop items for your game. Trader shop items define what can be purchased from in-game traders, their pricing, and any required progression keys.

## File Format
Trader items are defined in `.yml` files, with the filename matching the prefab ID of the trader. Below is a breakdown of each component in the configuration.

---

## Example Trader Item Configuration (Haldor.yml)
```yml
PrefabName: YmirFlesh
Stack: 1
Price: 120
RequiredKey: ""
```

---

## Configuration Fields

### General Settings
- **PrefabName** *(string)* – The unique name of the item being sold.
- **Stack** *(int)* – The number of items sold per purchase.
- **Price** *(int)* – The base cost of the item in trader currency.
- **RequiredKey** *(string)* – A prerequisite key required to unlock the item.

---

## Creating a New Trader Item
1. Copy the example trader item and modify the fields according to your desired setup.
2. Assign a unique `PrefabName` matching the in-game item.
3. Define the item’s `Stack` and `Price`.
4. Adjust `RequiredKey` if the item should be locked behind progression.
5. Save the file with the trader's prefab ID as the filename (e.g., `Haldor.yml`).
6. Place the file in the correct directory to be read by the game.

---

## Tips
- Ensure each `PrefabName` matches an existing in-game item.
- Use an empty `RequiredKey` (`""`) for items available at any time.
- Test your trader configuration in-game to confirm that all settings function correctly.

---

# Gambling Slot Machine Configuration Guide

This guide will help you configure gambling slot machines in your game. The gambling system allows players to spend currency for a chance to receive a randomized item.

## File Format
Gambling items are defined in `.yml` files, following a structured format. Below is a breakdown of each component in the configuration.

---

## Example Gamble Configuration
```yml
PrefabName: TraderCoin_RS
Amount: 1
Quality: 2
RequiredKey: ""
CurrencyPrefab: TraderCoin_RS
Price: 5
SuccessChance: 75.0
```

---

## Configuration Fields

### General Settings
- **PrefabName** *(string)* – The unique name of the item that can be won.
- **Amount** *(int)* – The number of items given upon success.
- **Quality** *(int)* – The quality level of the item.
- **RequiredKey** *(string)* – A prerequisite key required to unlock the gamble option.
- **CurrencyPrefab** *(string)* – The currency required for gambling.
- **Price** *(int)* – The cost of one gamble attempt.
- **SuccessChance** *(float)* – The percentage chance of winning the item.

---

## Creating a New Gamble Item
1. Copy the example gamble item and modify the fields according to your desired setup.
2. Assign a unique `PrefabName` matching the in-game item.
3. Define the item’s `Amount`, `Quality`, and `Price`.
4. Adjust `SuccessChance` to control the odds of winning the item.
5. Save the file with a `.yml` extension and place it in the appropriate directory.

---

## Tips
- Lower `SuccessChance` values create rarer rewards.
- Ensure `PrefabName` matches an existing in-game item.
- Use a balanced `Price` to maintain fairness in the gambling system.
- Test your gambling configuration in-game to confirm that all settings function correctly.

---

| `Version` | `Update Notes`    |
|-----------|-------------------|
| 1.0.0     | - Initial Release |