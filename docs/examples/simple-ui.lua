-- Simple Nagi appearance. Paste into Settings > Lua UI studio > Edit Lua UI.
-- Choose Apply script to save it and see the changes immediately.
-- Lines starting with -- are comments; they do not change the appearance.
return {
  name = "Simple red",              -- Name shown in Lua UI status.
  accent = "#FF4B4B",                -- Red highlight color: #RRGGBB.
  card_radius = 22,                   -- Rounded card corners: 8 to 28.
  spacing = 12,                       -- Space between overview sections: 6 to 20.
  labels = { cpu = "CPU activity" }  -- Rename the CPU metric.
}
-- Other options keep their defaults. Turn off Enable Lua UI to reset the look.
