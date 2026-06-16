ALTER TABLE "User"
    ADD CONSTRAINT check_tokens
        CHECK (Tokens >= 0);

ALTER TABLE "User"
    ADD CONSTRAINT check_escarowed_tokens
        CHECK (Escrowed_tokens >= 0);

ALTER TABLE "User"
    ADD CONSTRAINT check_experience
        CHECK (Experience >= 0);

ALTER TABLE "User"
    ADD CONSTRAINT check_birth_date
        CHECK (date_of_birth < now());

ALTER TABLE "User"
    ADD CONSTRAINT check_registration_date
        CHECK (registration_date <= now());

ALTER TABLE "User"
    ADD CONSTRAINT check_email_length
        CHECK (char_length(trim(email)) > 0);

ALTER TABLE Rate
    ADD CONSTRAINT mark_range_check
        CHECK (Mark >= 1 AND Mark <= 10);

ALTER TABLE Offer
    ADD CONSTRAINT check_token_cost
        CHECK (Token_Cost >= 0);

ALTER TABLE Offer
    ADD CONSTRAINT check_offered_tokens
        CHECK (Tokens_offered >= 0);

ALTER TABLE Offer
    ADD CONSTRAINT check_tokens_wanted
        CHECK (Tokens_wanted >= 0);

ALTER TABLE Offer
    ADD CONSTRAINT check_expiration_date
        CHECK (Exp_Date >= Creation_date);

ALTER TABLE Offer
    ADD CONSTRAINT check_offer_title
        CHECK (char_length(trim(title)) > 0);

ALTER TABLE Offer
    ADD CONSTRAINT check_offer_description
        CHECK (char_length(trim(description)) > 0);

ALTER TABLE Item
    ADD CONSTRAINT check_estimated_value
        CHECK (Estimated_token_value >= 0);

ALTER TABLE Item
    ADD CONSTRAINT check_item_name
        CHECK (char_length(trim(name)) > 0);

ALTER TABLE Item_rarity
    ADD CONSTRAINT check_rarity_name
        CHECK (char_length(trim(rarity_name)) > 0);

ALTER TABLE Listing_Items
    ADD CONSTRAINT check_quantity
        CHECK (Quantity > 0);

ALTER TABLE Listing_Counter_Offer_Items
    ADD CONSTRAINT check_co_quantity
        CHECK (Quantity > 0);

ALTER TABLE Counter_Offer
    ADD CONSTRAINT check_co_tokens
        CHECK (Tokens_Offered >= 0);

ALTER TABLE Notification
    ADD CONSTRAINT check_notification_title
        CHECK (char_length(trim(Title)) > 0);

ALTER TABLE Chat_messages
    ADD CONSTRAINT check_message_length
        CHECK (char_length(trim(Message)) > 0);

ALTER TABLE Trade
    ADD CONSTRAINT check_completion_date
        CHECK (
            completition_date IS NULL
                OR completition_date >= creation_date
            );

ALTER TABLE Genre
    ADD CONSTRAINT check_genre_name
        CHECK (char_length(trim(name)) > 0);

ALTER TABLE Game
    ADD CONSTRAINT check_game_name
        CHECK (char_length(trim(name)) > 0);

ALTER TABLE Notification
    ADD CONSTRAINT check_notification
        CHECK (char_length(trim(message)) > 0);

ALTER TABLE Emails
    ADD CONSTRAINT check_emails_subject
        CHECK (char_length(trim(subject)) > 0);

ALTER TABLE Emails
    ADD CONSTRAINT check_emails_body
        CHECK (char_length(trim(body)) > 0);

ALTER TABLE Chat_messages
    ADD CONSTRAINT check_edited_time
        CHECK (
            Edited_at IS NULL
                OR Edited_at >= Created_at
            );

ALTER TABLE Chat_messages
    ADD CONSTRAINT check_deleted
        CHECK (
            Deleted_at IS NULL
                OR Deleted_at >= Created_at
            );

ALTER TABLE Trade
    ADD CONSTRAINT check_middleman_not_customer
        CHECK (
            Middleman_User_Id IS NULL
                OR Middleman_User_Id != Customer_Id
            );

ALTER TABLE User_trade_stats
    ADD CONSTRAINT User_trade_stats_checks CHECK (
        successful_trades >= 0
            AND completed_trades >= successful_trades
            AND rating_sum >= 0
            AND rating_count >= 0
        );