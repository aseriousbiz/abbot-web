#!/bin/sh
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE USER abbotuser WITH PASSWORD 'h1w85AUfZYHS';
    CREATE DATABASE abbot;
    GRANT ALL PRIVILEGES ON DATABASE abbot TO abbotuser;
EOSQL