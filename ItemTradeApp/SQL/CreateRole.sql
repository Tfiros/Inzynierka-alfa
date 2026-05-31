CREATE ROLE CrossTrade_Api
    WITH LOGIN PASSWORD '#Zgadnij321!';


GRANT CONNECT
    ON DATABASE test_db
    TO CrossTrade_Api;

GRANT USAGE
    ON SCHEMA public
    TO CrossTrade_Api;

GRANT SELECT, INSERT, UPDATE
    ON ALL TABLES IN SCHEMA public
    TO CrossTrade_Api;

GRANT USAGE, SELECT
    ON ALL SEQUENCES IN SCHEMA public
    TO CrossTrade_Api;


GRANT DELETE ON TABLE
    "user_favourite_offer",
    "listing_items"
    TO CrossTrade_Api;

REVOKE INSERT, UPDATE, DELETE
    ON TABLE
    "offer_status",
    "trade_status"
    FROM CrossTrade_Api;