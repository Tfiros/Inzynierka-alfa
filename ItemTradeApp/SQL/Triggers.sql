CREATE OR REPLACE FUNCTION trg_update_user_trade_count() RETURNS trigger AS $$
DECLARE
    seller_id int;
    success_count int := 0;
    completed_count int := 0;
BEGIN

    IF NEW.trade_status_id = 3 AND OLD.trade_status_id = 2 THEN
        success_count := 1; completed_count := 1;
    ELSIF NEW.trade_status_id = 4 AND (OLD.trade_status_id = 2 OR OLD.trade_status_id = 1) THEN
        completed_count := 1;
    ELSE
        RETURN NEW;
    END IF;

    SELECT user_id INTO seller_id FROM offer WHERE id = NEW.offer_id;

    UPDATE user_trade_stats
    SET successful_trades = successful_trades + success_count,
        completed_trades = completed_trades + completed_count
    WHERE user_id = seller_id;

    IF NOT FOUND THEN
        RAISE WARNING 'user_trade_stats missing for user %', seller_id;
    END IF;


    RETURN NEW;
END;
$$ LANGUAGE plpgsql;



CREATE TRIGGER update_user_trade_count
    AFTER UPDATE OF trade_status_id ON trade
    FOR EACH ROW EXECUTE FUNCTION trg_update_user_trade_count();


CREATE OR REPLACE FUNCTION trg_update_user_rating_count() RETURNS trigger AS $$
BEGIN
    UPDATE user_trade_stats
    SET rating_sum = rating_sum + NEW.mark,
        rating_count = rating_count + 1
    WHERE user_id = NEW.user_id;

    IF NOT FOUND THEN
        RAISE WARNING 'user_trade_stats missing for user %', NEW.user_id;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;


CREATE TRIGGER update_user_rating_count
    AFTER INSERT ON rate
    FOR EACH ROW EXECUTE FUNCTION trg_update_user_rating_count();

