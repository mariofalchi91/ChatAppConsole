-- 1. CREAZIONE DEL SUPER ADMIN (Il tuo account "Dio")
-- Sostituirà 'cassandra'. Password lunga ma facile da ricordare.
CREATE ROLE admin_root 
WITH PASSWORD = 'IlGattoRossoSaltaIlFosso2026' 
AND SUPERUSER = true 
AND LOGIN = true;

-- 2. CREAZIONE DEL KEYSPACE (Obbligatorio crearlo ora)
-- Non possiamo dare permessi su un database che non esiste.
CREATE KEYSPACE IF NOT EXISTS chatapp 
WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1};

-- 3. CREAZIONE UTENTE DBA (Amministratore di chatapp)
-- Può creare tabelle, modificarle e cancellarle, ma SOLO dentro 'chatapp'.
CREATE ROLE dba_user 
WITH PASSWORD = 'GestioneStrutturaDB2026' 
AND SUPERUSER = false 
AND LOGIN = true;

GRANT ALL PERMISSIONS ON KEYSPACE chatapp TO dba_user;

-- 4. CREAZIONE UTENTE SERVER C# (Il "passacarte")
-- Può solo leggere (SELECT) e scrivere/modificare/cancellare dati (MODIFY).
-- NON può fare DROP TABLE o alterare la struttura.
CREATE ROLE chat_server 
WITH PASSWORD = 'ServerApplicativoChat2026' 
AND SUPERUSER = false 
AND LOGIN = true;

GRANT SELECT ON KEYSPACE chatapp TO chat_server;
GRANT MODIFY ON KEYSPACE chatapp TO chat_server;

-- 5. NEUTRALIZZAZIONE DELL'UTENTE DI DEFAULT
-- Cambiamo la password in una stringa a caso e gli togliamo ogni potere.
-- logout da cassandra cassandra e login con admin_root per eseguire questo comando.
ALTER ROLE cassandra 
WITH PASSWORD = 'password_scartata_inutilizzabile_999888777' 
AND SUPERUSER = false 
AND LOGIN = false;