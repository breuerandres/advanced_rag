using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260608100000_AddHierarchicalAccessBaseline")]
public partial class AddHierarchicalAccessBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            create table if not exists app.organizational_units (
                "Id" uuid primary key,
                name character varying(200) not null,
                parent_id uuid null references app.organizational_units("Id") on delete restrict,
                is_active boolean not null default true,
                created_at timestamp with time zone not null default now(),
                updated_at timestamp with time zone not null default now()
            );

            create table if not exists app.organizational_unit_closure (
                ancestor_id uuid not null references app.organizational_units("Id") on delete cascade,
                descendant_id uuid not null references app.organizational_units("Id") on delete cascade,
                depth integer not null,
                constraint "PK_organizational_unit_closure" primary key (ancestor_id, descendant_id)
            );

            create index if not exists "IX_organizational_units_parent_id"
                on app.organizational_units (parent_id);
            create unique index if not exists "IX_organizational_units_name"
                on app.organizational_units (name);
            create index if not exists "IX_organizational_unit_closure_descendant_id"
                on app.organizational_unit_closure (descendant_id);

            insert into app.organizational_units ("Id", name, parent_id, is_active, created_at, updated_at)
            values ('01000000-0000-0000-0000-000000000001', 'Empresa', null, true, now(), now())
            on conflict ("Id") do update set
                name = excluded.name,
                is_active = true,
                updated_at = now();

            insert into app.organizational_unit_closure (ancestor_id, descendant_id, depth)
            values (
                '01000000-0000-0000-0000-000000000001',
                '01000000-0000-0000-0000-000000000001',
                0
            )
            on conflict (ancestor_id, descendant_id) do update set depth = excluded.depth;

            do $$
            begin
                if to_regclass('app.users') is not null then
                    alter table app.users
                        add column if not exists organizational_unit_id uuid not null
                            default '01000000-0000-0000-0000-000000000001',
                        add column if not exists access_scope_version bigint not null default 1;

                    create index if not exists "IX_users_organizational_unit_id"
                        on app.users (organizational_unit_id);

                    if not exists (
                        select 1 from pg_constraint
                        where conname = 'FK_users_organizational_units_organizational_unit_id'
                          and conrelid = 'app.users'::regclass
                    ) then
                        alter table app.users
                            add constraint "FK_users_organizational_units_organizational_unit_id"
                            foreign key (organizational_unit_id)
                            references app.organizational_units("Id")
                            on delete restrict;
                    end if;
                end if;

                if to_regclass('app.groups') is not null then
                    alter table app.groups
                        add column if not exists owner_organizational_unit_id uuid null,
                        add column if not exists publishing_policy character varying(32) not null default 'OwnerScope';

                    create index if not exists "IX_groups_owner_organizational_unit_id"
                        on app.groups (owner_organizational_unit_id);

                    if not exists (
                        select 1 from pg_constraint
                        where conname = 'FK_groups_organizational_units_owner_organizational_unit_id'
                          and conrelid = 'app.groups'::regclass
                    ) then
                        alter table app.groups
                            add constraint "FK_groups_organizational_units_owner_organizational_unit_id"
                            foreign key (owner_organizational_unit_id)
                            references app.organizational_units("Id")
                            on delete restrict;
                    end if;
                end if;

                if to_regclass('app.document_permissions') is not null then
                    alter table app.document_permissions
                        add column if not exists organizational_unit_id uuid null;

                    create index if not exists "IX_document_permissions_organizational_unit_id"
                        on app.document_permissions (organizational_unit_id);

                    if not exists (
                        select 1 from pg_constraint
                        where conname = 'FK_document_permissions_organizational_units_organizational_unit_id'
                          and conrelid = 'app.document_permissions'::regclass
                    ) then
                        alter table app.document_permissions
                            add constraint "FK_document_permissions_organizational_units_organizational_unit_id"
                            foreign key (organizational_unit_id)
                            references app.organizational_units("Id")
                            on delete restrict;
                    end if;
                end if;

                if to_regclass('app.document_permissions') is not null
                   and to_regclass('app.groups') is not null then
                    create table if not exists app.document_permission_groups (
                        document_permission_id uuid not null references app.document_permissions("Id") on delete cascade,
                        group_id uuid not null references app.groups("Id") on delete cascade,
                        constraint "PK_document_permission_groups" primary key (document_permission_id, group_id)
                    );

                    create index if not exists "IX_document_permission_groups_group_id"
                        on app.document_permission_groups (group_id);

                    insert into app.document_permission_groups (document_permission_id, group_id)
                    select "Id", group_id
                    from app.document_permissions
                    where group_id is not null
                    on conflict do nothing;
                end if;

                if to_regclass('app.users') is not null
                   and to_regclass('app.groups') is not null then
                    create table if not exists app.user_group_publish_grants (
                        user_id uuid not null references app.users("Id") on delete cascade,
                        group_id uuid not null references app.groups("Id") on delete cascade,
                        granted_by_user_id uuid not null references app.users("Id") on delete restrict,
                        created_at timestamp with time zone not null default now(),
                        constraint "PK_user_group_publish_grants" primary key (user_id, group_id)
                    );

                    create index if not exists "IX_user_group_publish_grants_group_id"
                        on app.user_group_publish_grants (group_id);
                    create index if not exists "IX_user_group_publish_grants_granted_by_user_id"
                        on app.user_group_publish_grants (granted_by_user_id);
                end if;

                if to_regclass('app.roles') is not null then
                    insert into app.roles ("Id", name) values
                        ('00000000-0000-0000-0000-0000000000a4', 'DocumentEditor'),
                        ('00000000-0000-0000-0000-0000000000a5', 'DocumentPublisher')
                    on conflict (name) do nothing;

                    if to_regclass('app.user_roles') is not null then
                        delete from app.roles roles
                        where roles.name = 'DocumentManager'
                          and not exists (
                              select 1 from app.user_roles user_roles
                              where user_roles.role_id = roles."Id"
                          );
                    else
                        delete from app.roles where name = 'DocumentManager';
                    end if;
                end if;

                if exists(select 1 from pg_roles where rolname = 'rag_owner') then
                    if to_regclass('app.document_permissions') is not null then
                        grant select on table app.document_permissions to rag_owner;
                    end if;
                    if to_regclass('app.document_permission_groups') is not null then
                        grant select on table app.document_permission_groups to rag_owner;
                    end if;
                    grant select on table app.organizational_units to rag_owner;
                    grant select on table app.organizational_unit_closure to rag_owner;
                    if to_regclass('app.groups') is not null then
                        grant select on table app.groups to rag_owner;
                    end if;
                    if to_regclass('app.user_ai_budget_limits') is not null then
                        grant select on table app.user_ai_budget_limits to rag_owner;
                    end if;
                end if;
            end $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if exists(select 1 from pg_roles where rolname = 'rag_owner') then
                    if to_regclass('app.document_permission_groups') is not null then
                        revoke select on table app.document_permission_groups from rag_owner;
                    end if;
                    revoke select on table app.organizational_units from rag_owner;
                    revoke select on table app.organizational_unit_closure from rag_owner;
                    if to_regclass('app.groups') is not null then
                        revoke select on table app.groups from rag_owner;
                    end if;
                end if;

                drop table if exists app.document_permission_groups;
                drop table if exists app.user_group_publish_grants;

                if to_regclass('app.document_permissions') is not null then
                    alter table app.document_permissions
                        drop constraint if exists "FK_document_permissions_organizational_units_organizational_unit_id";
                    drop index if exists app."IX_document_permissions_organizational_unit_id";
                    alter table app.document_permissions
                        drop column if exists organizational_unit_id;
                end if;

                if to_regclass('app.groups') is not null then
                    alter table app.groups
                        drop constraint if exists "FK_groups_organizational_units_owner_organizational_unit_id";
                    drop index if exists app."IX_groups_owner_organizational_unit_id";
                    alter table app.groups
                        drop column if exists owner_organizational_unit_id,
                        drop column if exists publishing_policy;
                end if;

                if to_regclass('app.users') is not null then
                    alter table app.users
                        drop constraint if exists "FK_users_organizational_units_organizational_unit_id";
                    drop index if exists app."IX_users_organizational_unit_id";
                    alter table app.users
                        drop column if exists access_scope_version,
                        drop column if exists organizational_unit_id;
                end if;

                drop table if exists app.organizational_unit_closure;
                drop table if exists app.organizational_units;

                if to_regclass('app.roles') is not null then
                    delete from app.roles where name in ('DocumentEditor', 'DocumentPublisher');
                    insert into app.roles ("Id", name)
                    values ('00000000-0000-0000-0000-0000000000a2', 'DocumentManager')
                    on conflict (name) do nothing;
                end if;
            end $$;
            """);
    }
}
