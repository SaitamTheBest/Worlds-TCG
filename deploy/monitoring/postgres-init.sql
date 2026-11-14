-- Only the one-shot initializer receives the application database credentials.
-- psql quotes the password as a SQL literal (never concatenate it into SQL).
\getenv monitor_password MONITOR_PASSWORD
SELECT 'CREATE ROLE worlds_monitor LOGIN'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'worlds_monitor') \gexec
ALTER ROLE worlds_monitor WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD :'monitor_password';
GRANT pg_monitor TO worlds_monitor;
ALTER ROLE worlds_monitor SET default_transaction_read_only = on;
