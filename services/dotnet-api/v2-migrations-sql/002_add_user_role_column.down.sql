-- Reverse of 002_add_user_role_column

DROP INDEX IF EXISTS app.ix_users_role;
ALTER TABLE app.users DROP COLUMN IF EXISTS role;
