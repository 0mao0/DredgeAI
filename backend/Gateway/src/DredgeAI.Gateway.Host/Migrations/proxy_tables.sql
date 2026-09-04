CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904092330_Add_proxy_tables') THEN
    CREATE TABLE tab_proxy_cluster (
        f_id uuid NOT NULL,
        f_cluster_id character varying(128) NOT NULL,
        f_destinations_json text NOT NULL,
        f_extra_properties text NOT NULL,
        f_concurrency_stamp character varying(40) NOT NULL,
        f_creation_time timestamp with time zone NOT NULL,
        f_creator_id uuid,
        f_last_modification_time timestamp with time zone,
        f_last_modifier_id uuid,
        f_is_deleted boolean NOT NULL DEFAULT FALSE,
        f_deleter_id uuid,
        f_deletion_time timestamp with time zone,
        CONSTRAINT "PK_tab_proxy_cluster" PRIMARY KEY (f_id)
    );
    COMMENT ON COLUMN tab_proxy_cluster.f_id IS '主键Id';
    COMMENT ON COLUMN tab_proxy_cluster.f_cluster_id IS 'YARP 集群 ID，全局唯一';
    COMMENT ON COLUMN tab_proxy_cluster.f_destinations_json IS '目的地字典 JSON（destinationId → 下游地址）';
    COMMENT ON COLUMN tab_proxy_cluster.f_extra_properties IS '拓展字段';
    COMMENT ON COLUMN tab_proxy_cluster.f_concurrency_stamp IS '并发标识';
    COMMENT ON COLUMN tab_proxy_cluster.f_creation_time IS '创建时间';
    COMMENT ON COLUMN tab_proxy_cluster.f_creator_id IS '创建人ID';
    COMMENT ON COLUMN tab_proxy_cluster.f_last_modification_time IS '修改时间';
    COMMENT ON COLUMN tab_proxy_cluster.f_last_modifier_id IS '修改者ID';
    COMMENT ON COLUMN tab_proxy_cluster.f_is_deleted IS '是否删除 0.否 1.是';
    COMMENT ON COLUMN tab_proxy_cluster.f_deleter_id IS '删除者ID';
    COMMENT ON COLUMN tab_proxy_cluster.f_deletion_time IS '删除时间';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904092330_Add_proxy_tables') THEN
    CREATE TABLE tab_proxy_route (
        f_id uuid NOT NULL,
        f_route_id character varying(128) NOT NULL,
        f_cluster_id character varying(128) NOT NULL,
        f_order integer NOT NULL,
        f_match_path character varying(256) NOT NULL,
        f_match_hosts_json text,
        f_match_methods_json text,
        f_authorization_policy character varying(64) NOT NULL,
        f_is_enabled boolean NOT NULL,
        f_extra_properties text NOT NULL,
        f_concurrency_stamp character varying(40) NOT NULL,
        f_creation_time timestamp with time zone NOT NULL,
        f_creator_id uuid,
        f_last_modification_time timestamp with time zone,
        f_last_modifier_id uuid,
        f_is_deleted boolean NOT NULL DEFAULT FALSE,
        f_deleter_id uuid,
        f_deletion_time timestamp with time zone,
        CONSTRAINT "PK_tab_proxy_route" PRIMARY KEY (f_id)
    );
    COMMENT ON COLUMN tab_proxy_route.f_id IS '主键Id';
    COMMENT ON COLUMN tab_proxy_route.f_route_id IS 'YARP 路由 ID，全局唯一';
    COMMENT ON COLUMN tab_proxy_route.f_cluster_id IS '引用的集群 ID';
    COMMENT ON COLUMN tab_proxy_route.f_order IS '路由匹配优先级（值越小越优先）';
    COMMENT ON COLUMN tab_proxy_route.f_match_path IS '路径匹配模式，如 /api/compare/{**catch-all}';
    COMMENT ON COLUMN tab_proxy_route.f_match_hosts_json IS 'Host 匹配列表 JSON 数组，null 表示不限制';
    COMMENT ON COLUMN tab_proxy_route.f_match_methods_json IS 'HTTP 方法匹配列表 JSON 数组，null 表示不限制';
    COMMENT ON COLUMN tab_proxy_route.f_authorization_policy IS '授权策略（YARP 内置字面量 anonymous 或 default）';
    COMMENT ON COLUMN tab_proxy_route.f_is_enabled IS '是否启用；禁用的路由不进 YARP 快照';
    COMMENT ON COLUMN tab_proxy_route.f_extra_properties IS '拓展字段';
    COMMENT ON COLUMN tab_proxy_route.f_concurrency_stamp IS '并发标识';
    COMMENT ON COLUMN tab_proxy_route.f_creation_time IS '创建时间';
    COMMENT ON COLUMN tab_proxy_route.f_creator_id IS '创建人ID';
    COMMENT ON COLUMN tab_proxy_route.f_last_modification_time IS '修改时间';
    COMMENT ON COLUMN tab_proxy_route.f_last_modifier_id IS '修改者ID';
    COMMENT ON COLUMN tab_proxy_route.f_is_deleted IS '是否删除 0.否 1.是';
    COMMENT ON COLUMN tab_proxy_route.f_deleter_id IS '删除者ID';
    COMMENT ON COLUMN tab_proxy_route.f_deletion_time IS '删除时间';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904092330_Add_proxy_tables') THEN
    CREATE UNIQUE INDEX "IX_tab_proxy_cluster_f_cluster_id" ON tab_proxy_cluster (f_cluster_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904092330_Add_proxy_tables') THEN
    CREATE UNIQUE INDEX "IX_tab_proxy_route_f_route_id" ON tab_proxy_route (f_route_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260904092330_Add_proxy_tables') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260904092330_Add_proxy_tables', '10.0.9');
    END IF;
END $EF$;
COMMIT;

