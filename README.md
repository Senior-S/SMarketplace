# SMarketplace
Allow players to sell items between them!

The plugin requires a MySQL database and a workshop UI.<br>
[Link to the workshop UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3199423243)

[Plugin preview](https://youtu.be/MunakfiNmSk)

## Commands
`/Marketplace` - Permission: 'ss.command.Marketplace'

`/ListItem` - Permission: 'ss.command.ListItem'

### Configuration
```xml
  <hexDefaultMessagesColor>#2BC415</hexDefaultMessagesColor>
  <requiredItemToMarketplace>0</requiredItemToMarketplace>
  <uiEffectID>51300</uiEffectID>
  <uiEffectKey>31300</uiEffectKey>
  <useUconomy>true</useUconomy>
  <iconsCDN>https://cdn.lyhme.gg/items/{0}.png</iconsCDN>
  <blacklistedItems>
    <ItemID>519</ItemID>
  </blacklistedItems>
  <dbServer>127.0.0.1</dbServer>
  <dbPort>3306</dbPort>
  <dbUser>root</dbUser>
  <dbPassword>toor</dbPassword>
  <dbDatabase>unturned</dbDatabase>
```