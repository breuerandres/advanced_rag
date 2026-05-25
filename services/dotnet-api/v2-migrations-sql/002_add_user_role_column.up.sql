-- v2 migration 002: promote role to first-class column on users
-- See docs/adr/0006-unified-session-auth.md

ALTER TABLE app.users
    ADD COLUMN IF NOT EXISTS role text NOT NULL DEFAULT 'viewer'
        CHECK (role IN ('admin', 'editor', 'viewer'));

-- Backfill from existing user_roles join table where present.
UPDATE app.users u
   SET role = CASE
                  WHEN EXISTS (
                      SELECT 1 FROM app.user_roles ur
                      JOIN app.roles r ON r.id = ur.role_id
                      WHERE ur.user_id = u.id AND r.name = 'Admin'
                  ) THEN 'admin'
                  WHEN EXISTS (
                      SELECT 1 FROM app.user_roles ur
                      JOIN app.roles r ON r.id = ur.role_id
                      WHERE ur.user_id = u.id AND r.name = 'DocumentManager'
                  ) THEN 'editor'
                  ELSE 'viewer'
              END;

CREATE INDEX IF NOT EXISTS ix_users_role ON app.users (role);

-- Note: the user_roles + roles tables are NOT dropped yet. A future migration removes
-- them after a release of dual-write compatibility. Until then, .NET code reads the new
-- column and ignores user_roles.
