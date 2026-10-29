# XivHud.Crafting

Development recipe browser and session worklist for the Umbra-centered interface.
`/xivcrafting` opens Workshop, `close` hides it, `preview` uses sample recipes and
an independent sample worklist, and `settings` opens the same Workshop.
`/xivnav` lists it under Crafting and gathering.

Browse the local Recipe sheet by output name, ingredient name and discipline.
The paged list shows level; recipe details show output icon, output yield and all
direct ingredients. Add 1–999 **crafts** per recipe, up to 100 distinct recipes.
Worklist totals aggregate shared ingredients before subtracting NQ + HQ stock in
the four carried bags, excluding collectables. Missing quantities never fall below zero. Partial scans
show unknown owned/missing quantities. Crystals always show unknown because the
crystal container is outside this bag scan. Copy the material list to the clipboard.

Plans live only in memory and clear at logout or unload. Preview plans cannot
change the live plan. The catalogue includes locked recipes and does not evaluate
unlock, specialist, job level or quest requirements. Check the native crafting log
before crafting. Subrecipes are not expanded; this is planning, not a crafting
executor, gathering navigator or synthesis-progress replacement.

`XivHud.Crafting.v1.Call` supports cached status and queued screen-window controls
only. It cannot edit plans or start a craft. No network requests, player uploads
or game actions are performed. Native crafting remains available.

Offline tests cover shared-stock aggregation, craft counts versus output yield,
partial scans, crystals, overflow bounds and excess stock. All live reads use the
family's reviewed inventory binding guard. Recipe decoding, UI/controller use,
material accuracy and unload/reload still require in-game acceptance. The ZIP is
listed only in the explicit development catalogue.
