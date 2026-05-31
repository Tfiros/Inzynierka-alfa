INSERT INTO "Genre" ("Name", "Is_Deleted")
VALUES
    ('FPS', false),
    ('RPG', false),
    ('MMORPG', false),
    ('Action', false);

INSERT INTO "Game" ("Name", "Photo_URLl", "Genre_Id", "Is_Deleted")
VALUES
    ('Counter-Strike 2', null, 1, false),
    ('World of Warcraft', null, 3, false),
    ('Warframe', null, 3, false),
    ('Team Fortress 2', null, 1, false);

INSERT INTO "Item_rarity"
("Game_Id", "Rarity_name", "Is_Deleted")
VALUES

    (1, 'Consumer Grade', false),
    (1, 'Industrial Grade', false),
    (1, 'Mil-Spec', false),
    (1, 'Restricted', false),
    (1, 'Classified', false),
    (1, 'Covert', false),

    (2, 'Poor', false),
    (2, 'Common', false),
    (2, 'Uncommon', false),
    (2, 'Rare', false),
    (2, 'Epic', false),
    (2, 'Legendary', false),
    (2, 'Artifact', false),
    (2, 'Heirloom', false),
    (2, 'WoW Token', false),

    (3, 'Common', false),
    (3, 'Uncommon', false),
    (3, 'Rare', false),
    (3, 'Legendary', false),

    (4, 'stock', false),
    (4, 'unique', false),
    (4, 'vintage', false),
    (4, 'genuine', false),
    (4, 'strange', false),
    (4, 'unusual', false),
    (4, 'haunted', false),
    (4, 'collectors:', false),
    (4, 'decorated', false),
    (4, 'community', false),
    (4, 'self-made', false),
    (4, 'valve', false);

INSERT INTO "Item"
("Game_Id", "Item_rarity_Id", "Name", "Photo_URL", "Is_Deleted", "Estimated_token_value")
VALUES
    (1, 6, 'AK-47 Aquamarine Revenge', null, false, 120),
    (1, 6, 'AWP Dragon Lore', null, false, 250),
    (1, 6, 'M9 Bayonet Doppler', null, false, 180),
    (1, 6, 'Karambit Doppler', null, false, 900),

    (2, 7, 'Thunderfury, Blessed Blade of the Windseeker', null, false, 700),
    (2, 8, 'Enchant Weapon – Spell Power', null, false, 500),
    (2, 9, 'Ironfoe', null, false, 650),
    (2, 10, 'Teebu’s Blazing Longsword', null, false, 1200),

    (3, 16, 'AX-52', null, false, 150),
    (3, 17, 'Broken War', null, false, 220),
    (3, 18, 'Burston', null, false, 170),
    (3, 19, 'Cadus', null, false, 350),

    (4, 20, 'Familiar Fez', null, false, 50),
    (4, 21, 'Night Vision Gawkers', null, false, 300),
    (4, 22, 'Frostbite Bonnet', null, false, 180),
    (4, 23, 'Misdirector', null, false, 400);


INSERT INTO "Offer"
(
    "User_Id",
    "Exp_Date",
    "Creation_date",
    "Token_Cost",
    "Offer_Status_Id",
    "Title",
    "Description",
    "Is_highlighted",
    "Tokens_offered",
    "Tokens_wanted"
)
VALUES
    (1, CURRENT_DATE + 30, CURRENT_DATE, 100, 1, 'CS2 skin trade', 'Szukam noża', false, 100, 150),
    (2, CURRENT_DATE + 30, CURRENT_DATE, 200, 1, 'WoW items', 'Rare itemy', true, 200, 250),
    (3, CURRENT_DATE + 30, CURRENT_DATE, 300, 1, 'Warframe stuff', 'Prime części', false, 300, 350),
    (1, CURRENT_DATE + 30, CURRENT_DATE, 150, 1, 'TF2 hats', 'Czapki unusual', false, 150, 180),
    (2, CURRENT_DATE + 30, CURRENT_DATE, 120, 1, 'CS skins', 'Trade skinów', true, 120, 140),
    (3, CURRENT_DATE + 30, CURRENT_DATE, 90, 1, 'WoW gold', 'Gold + itemy', false, 90, 130),
    (1, CURRENT_DATE + 30, CURRENT_DATE, 400, 1, 'Warframe mods', 'Riveny', true, 400, 500),
    (2, CURRENT_DATE + 30, CURRENT_DATE, 80, 1, 'TF2 trade', 'Random itemy', false, 80, 100),
    (3, CURRENT_DATE + 30, CURRENT_DATE, 220, 1, 'CS inventory', 'Sprzedam inventory', true, 220, 260),
    (1, CURRENT_DATE + 30, CURRENT_DATE, 110, 1, 'WoW mounts', 'Mounty', false, 110, 150),
    (2, CURRENT_DATE + 30, CURRENT_DATE, 160, 1, 'Warframe account', 'Stuff na konto', false, 160, 200),
    (3, CURRENT_DATE + 30, CURRENT_DATE, 500, 1, 'TF2 unusual', 'Unusual hat', true, 500, 600),
    (1, CURRENT_DATE + 30, CURRENT_DATE, 130, 1, 'CS knife', 'Knife trade', false, 130, 180),
    (2, CURRENT_DATE + 30, CURRENT_DATE, 70, 1, 'WoW gear', 'Raid gear', false, 70, 120),
    (3, CURRENT_DATE + 30, CURRENT_DATE, 210, 1, 'Warframe prime', 'Prime frame', true, 210, 280),
    (1, CURRENT_DATE + 30, CURRENT_DATE, 95, 1, 'TF2 cosmetics', 'Kosmetyki', false, 95, 110),
    (2, CURRENT_DATE + 30, CURRENT_DATE, 330, 1, 'CS2 inventory', 'Full eq', true, 330, 450),
    (3, CURRENT_DATE + 30, CURRENT_DATE, 140, 1, 'WoW classic', 'Classic itemy', false, 140, 170),
    (1, CURRENT_DATE + 30, CURRENT_DATE, 175, 1, 'Warframe relics', 'Relicsy', false, 175, 220),
    (2, CURRENT_DATE + 30, CURRENT_DATE, 260, 1, 'TF2 hats trade', 'Hatki', true, 260, 320);

INSERT INTO "Listing_Items"
("Offer_Id", "Item_Id", "Quantity", "Is_wanted")
VALUES

    (1, 1, 1, false),
    (1, 2, 1, true),

    (2, 5, 1, false),
    (2, 6, 1, true),

    (3, 9, 1, false),
    (3, 10, 1, true),

    (4, 13, 1, false),
    (4, 14, 1, true),

    (5, 3, 1, false),
    (5, 4, 1, true),

    (6, 7, 1, false),
    (6, 8, 1, true),

    (7, 11, 1, false),
    (7, 12, 1, true),

    (8, 15, 1, false),
    (8, 16, 1, true),

    (9, 2, 1, false),
    (9, 1, 1, true),

    (10, 6, 1, false),
    (10, 5, 1, true),

    (11, 10, 1, false),
    (11, 9, 1, true),

    (12, 14, 1, false),
    (12, 13, 1, true),

    (13, 4, 1, false),
    (13, 3, 1, true),

    (14, 8, 1, false),
    (14, 7, 1, true),

    (15, 12, 1, false),
    (15, 11, 1, true),

    (16, 16, 1, false),
    (16, 15, 1, true),

    (17, 1, 1, false),
    (17, 4, 1, true),

    (18, 5, 1, false),
    (18, 8, 1, true),

    (19, 9, 1, false),
    (19, 12, 1, true),

    (20, 13, 1, false),
    (20, 16, 1, true);