#!/bin/sh
# Runs once when the PostgreSQL volume is first initialised.
# Creates the logically separated sharing role and database in the same server.
set -eu
: "${SHARING_DB_PASSWORD:?Set SHARING_DB_PASSWORD}"
psql -v ON_ERROR_STOP=1 -v pw="$SHARING_DB_PASSWORD" -v db="$POSTGRES_DB" -U "$POSTGRES_USER" -d postgres <<'SQL'
CREATE ROLE sharing LOGIN PASSWORD :'pw';
CREATE DATABASE sharing OWNER sharing;
REVOKE CONNECT ON DATABASE sharing FROM PUBLIC;
REVOKE CONNECT ON DATABASE :"db" FROM PUBLIC;
SQL
