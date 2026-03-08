USE chatapp;
-- 1. TABELLA UTENTI
-- Contiene le credenziali, l'ultimo logout e i ReadWatermarks (nativamente!)
CREATE TABLE IF NOT EXISTS users (
    username text PRIMARY KEY,
    password text,
    last_logout timestamp,
    read_watermarks map<text, timestamp>
);
-- 2. TABELLA MESSAGGI PRIVATI
-- Partition Key: room_id (es. stringa ordinata "alberto_mario")
-- Clustering Keys: timestamp (per l'ordine) e id (per l'unicità assoluta)
DROP TABLE IF EXISTS private_messages;
CREATE TABLE IF NOT EXISTS private_messages (
    room_id text,
    timestamp timestamp,
    id uuid,
    sender text,
    receiver text,
    content text,
    msg_type int,      -- Mappa l'enum MessageType
    is_read boolean,
    PRIMARY KEY ((room_id), timestamp, id)
) WITH CLUSTERING ORDER BY (timestamp ASC);
-- 3. TABELLA MESSAGGI PUBBLICI
-- Usiamo 'channel_name' (es. "general") per raggrupparli
DROP TABLE IF EXISTS public_messages;
CREATE TABLE IF NOT EXISTS public_messages (
    channel_name text,
    timestamp timestamp,
    id uuid,
    sender text,
    content text,
    msg_type int,
    PRIMARY KEY ((channel_name), timestamp, id)
) WITH CLUSTERING ORDER BY (timestamp ASC);
-- 4. TABELLA BLOCCHI (Chi ho bloccato io?)
-- Risponde a: GetBlockedUsers(blocker) e IsBlocked(blocker, blocked)
CREATE TABLE IF NOT EXISTS user_blocks (
    blocker text,
    blocked text,
    created_at timestamp,
    PRIMARY KEY ((blocker), blocked)
);
-- 5. TABELLA BLOCCHI REVERSE (Chi mi ha bloccato?)
-- Risponde a: GetUsersWhoBlockedMe(username)
CREATE TABLE IF NOT EXISTS blocked_by_users (
    blocked text,
    blocker text,
    created_at timestamp,
    PRIMARY KEY ((blocked), blocker)
);

-- 6. TABELLA NOTIFICHE NON LETTE
CREATE TABLE IF NOT EXISTS unread_notifications (
    receiver text,
    sender text,
    PRIMARY KEY (receiver, sender)
);