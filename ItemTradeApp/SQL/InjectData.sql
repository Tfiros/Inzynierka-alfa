INSERT INTO offer_status (id, status_name)
VALUES
    (1, 'Active'),
    (2, 'Expired'),
    (3, 'InRealization'),
    (4, 'Completed'),
    (5, 'Canceled');

INSERT INTO counter_offer_status (id, status_name)
VALUES
    (1, 'Pending'),
    (2, 'Accepted'),
    (3, 'Denied');

INSERT INTO trade_status (id, status_name)
VALUES
    (1, 'New'),
    (2, 'InRealization'),
    (3, 'SuccesfulRealization'),
    (4, 'Failed');

INSERT INTO genre ("name", "is_deleted")
VALUES
    ('FPS', false),
    ('RPG', false),
    ('MMORPG', false),
    ('Action', false),
    ('Hack & Slash', false),
    ('Sport',false);

INSERT INTO game ("name", "photo_url", "genre_id", "is_deleted")
VALUES
    ('Counter-Strike 2', null, 1, false),
    ('World of Warcraft', null, 3, false),
    ('Warframe', null, 3, false),
    ('Team Fortress 2', null, 1, false),
    ('Fifa',null,6, false),
    ('Path Of Exile 2', null,5, false);

INSERT INTO item_rarity
("game_id", "rarity_name", "is_deleted")
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
    (4, 'valve', false),

    (5, 'Common', false),
    (5, 'Rare', false),
    (5, 'Icon', false),
    (5, 'Tots champion', false),

    (6, 'Normal', false),
    (6, 'Magic', false),
    (6, 'Rare', false),
    (6, 'Unique', false);

INSERT INTO item
("game_id", "item_rarity_id", "name", "photo_url", "is_deleted", "estimated_token_value")
VALUES
    (1, 6, 'AK-47 Aquamarine Revenge', null, false, 120),
    (1, 6, 'AWP Dragon Lore', null, false, 250),
    (1, 6, 'M9 Bayonet Doppler', null, false, 180),
    (1, 6, 'Karambit Doppler', null, false, 900),
    (1, 6, 'Butterfly Lure', null, false, 920),
    (1, 6, 'Butterfly Gamma Doppler', null, false, 900),

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
    (4, 23, 'Misdirector', null, false, 400),

    (5, 24, 'Lukasz Piszczek', null, false, 50),
    (5, 25, 'Lionel Messi', null, false, 300),
    (5, 26, 'Wojtek Szczesny', null, false, 350),
    (5, 27, 'Robert Lewandowski', null, false, 400),

    (5, 28, 'Facebreaker Stocky Mitts', null, false, 200),
    (5, 29, 'Northpaw Suede Bracers', null, false, 300),
    (5, 30, 'Horror''s Flight Engraved Bracers', null, false, 180),
    (5, 31, 'Aurseize Layered Gauntlets', null, false, 400);
