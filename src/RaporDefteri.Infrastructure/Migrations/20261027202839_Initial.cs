using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaporDefteri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rapor");

            migrationBuilder.CreateSequence(
                name: "report_entry_number_seq",
                schema: "rapor");

            migrationBuilder.CreateTable(
                name: "admission_detail",
                schema: "rapor",
                columns: table => new
                {
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inmate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custody_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_detail", x => x.report_entry_id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "rapor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "rapor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_event",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_name = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_log",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    primary_path = table.Column<string>(type: "text", nullable: false),
                    secondary_path = table.Column<string>(type: "text", nullable: false),
                    check_command = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "general_event_detail",
                schema: "rapor",
                columns: table => new
                {
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    follow_up = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_general_event_detail", x => x.report_entry_id);
                });

            migrationBuilder.CreateTable(
                name: "handover_note_detail",
                schema: "rapor",
                columns: table => new
                {
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handover_note_detail", x => x.report_entry_id);
                });

            migrationBuilder.CreateTable(
                name: "inmate",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_number = table.Column<string>(type: "text", nullable: false),
                    normalized_file_number = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    normalized_full_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inmate", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "material_detail",
                schema: "rapor",
                columns: table => new
                {
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    counterparty = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_detail", x => x.report_entry_id);
                });

            migrationBuilder.CreateTable(
                name: "release_detail",
                schema: "rapor",
                columns: table => new
                {
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inmate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custody_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_detail", x => x.report_entry_id);
                });

            migrationBuilder.CreateTable(
                name: "report_entry",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_number = table.Column<string>(type: "text", nullable: false),
                    event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entry_type = table.Column<int>(type: "integer", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_text = table.Column<string>(type: "text", nullable: false),
                    original_report_text = table.Column<string>(type: "text", nullable: false),
                    additional_note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    late_entry_reason = table.Column<string>(type: "text", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_snapshot",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_snapshot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    opened_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    handover_note = table.Column<string>(type: "text", nullable: true),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prepared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    close_note = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_gate",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active_shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_gate", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transfer_detail",
                schema: "rapor",
                columns: table => new
                {
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inmate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custody_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_institution = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_detail", x => x.report_entry_id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_card",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate_display = table.Column<string>(type: "text", nullable: false),
                    normalized_plate = table.Column<string>(type: "text", nullable: false),
                    base_location = table.Column<int>(type: "integer", nullable: false),
                    organization = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_card", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_type",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_visit",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_location_snapshot = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    location_inference = table.Column<int>(type: "integer", nullable: false),
                    start_direction = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_direction = table.Column<int>(type: "integer", nullable: true),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    driver_name = table.Column<string>(type: "text", nullable: true),
                    organization_snapshot = table.Column<string>(type: "text", nullable: true),
                    start_report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    end_report_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_visit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "visitor_detail",
                schema: "rapor",
                columns: table => new
                {
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    organization = table.Column<string>(type: "text", nullable: true),
                    purpose = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitor_detail", x => x.report_entry_id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "rapor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "rapor",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "rapor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "rapor",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "rapor",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "rapor",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "rapor",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "rapor",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "rapor",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "rapor",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "rapor",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "custody_period",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inmate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward = table.Column<string>(type: "text", nullable: true),
                    normalized_ward = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    end_note = table.Column<string>(type: "text", nullable: true),
                    admission_report_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    end_report_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custody_period", x => x.id);
                    table.ForeignKey(
                        name: "FK_custody_period_inmate_inmate_id",
                        column: x => x.inmate_id,
                        principalSchema: "rapor",
                        principalTable: "inmate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inmate_file_history",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inmate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_number = table.Column<string>(type: "text", nullable: false),
                    normalized_file_number = table.Column<string>(type: "text", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inmate_file_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_inmate_file_history_inmate_inmate_id",
                        column: x => x.inmate_id,
                        principalSchema: "rapor",
                        principalTable: "inmate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "temporary_absence",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inmate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custody_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    destination = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    departed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expected_return_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    departure_report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_report_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    departure_vehicle_visit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_vehicle_visit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    escort_note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    departure_shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temporary_absence", x => x.id);
                    table.ForeignKey(
                        name: "FK_temporary_absence_inmate_inmate_id",
                        column: x => x.inmate_id,
                        principalSchema: "rapor",
                        principalTable: "inmate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_entry_inmate",
                schema: "rapor",
                columns: table => new
                {
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inmate_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_entry_inmate", x => new { x.report_entry_id, x.inmate_id });
                    table.ForeignKey(
                        name: "FK_report_entry_inmate_report_entry_report_entry_id",
                        column: x => x.report_entry_id,
                        principalSchema: "rapor",
                        principalTable: "report_entry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_revision",
                schema: "rapor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revised_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    old_values = table.Column<string>(type: "text", nullable: false),
                    new_values = table.Column<string>(type: "text", nullable: false),
                    old_report_text = table.Column<string>(type: "text", nullable: false),
                    new_report_text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_revision", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_revision_report_entry_report_entry_id",
                        column: x => x.report_entry_id,
                        principalSchema: "rapor",
                        principalTable: "report_entry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shift_duty",
                schema: "rapor",
                columns: table => new
                {
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_duty", x => new { x.shift_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_shift_duty_shift_shift_id",
                        column: x => x.shift_id,
                        principalSchema: "rapor",
                        principalTable: "shift",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "rapor",
                table: "shift_gate",
                columns: new[] { "id", "active_shift_id" },
                values: new object[] { 1, null });

            migrationBuilder.InsertData(
                schema: "rapor",
                table: "vehicle_type",
                columns: new[] { "id", "is_active", "name", "sort_order" },
                values: new object[,]
                {
                    { new Guid("a1111111-1111-4111-8111-111111111101"), true, "Yemek aracı", 1 },
                    { new Guid("a1111111-1111-4111-8111-111111111102"), true, "Ring/sevk aracı", 2 },
                    { new Guid("a1111111-1111-4111-8111-111111111103"), true, "Ambulans", 3 },
                    { new Guid("a1111111-1111-4111-8111-111111111104"), true, "Malzeme/tedarik aracı", 4 },
                    { new Guid("a1111111-1111-4111-8111-111111111105"), true, "Bakım/teknik servis aracı", 5 },
                    { new Guid("a1111111-1111-4111-8111-111111111106"), true, "Resmî araç", 6 },
                    { new Guid("a1111111-1111-4111-8111-111111111107"), true, "Diğer", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "rapor",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "rapor",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "rapor",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "rapor",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "rapor",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "rapor",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "rapor",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_active_custody",
                schema: "rapor",
                table: "custody_period",
                column: "inmate_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_inmate_normalized_file_number",
                schema: "rapor",
                table: "inmate",
                column: "normalized_file_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inmate_file_history_inmate_id",
                schema: "rapor",
                table: "inmate_file_history",
                column: "inmate_id");

            migrationBuilder.CreateIndex(
                name: "IX_inmate_file_history_normalized_file_number",
                schema: "rapor",
                table: "inmate_file_history",
                column: "normalized_file_number");

            migrationBuilder.CreateIndex(
                name: "IX_report_entry_event_at",
                schema: "rapor",
                table: "report_entry",
                column: "event_at");

            migrationBuilder.CreateIndex(
                name: "IX_report_entry_record_number",
                schema: "rapor",
                table: "report_entry",
                column: "record_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_entry_shift_id",
                schema: "rapor",
                table: "report_entry",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "ux_report_entry_idempotency",
                schema: "rapor",
                table: "report_entry",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_revision_report_entry_id",
                schema: "rapor",
                table: "report_revision",
                column: "report_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_snapshot_kind_shift_id_period_start_version",
                schema: "rapor",
                table: "report_snapshot",
                columns: new[] { "kind", "shift_id", "period_start", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_open_absence",
                schema: "rapor",
                table: "temporary_absence",
                column: "inmate_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_card_normalized_plate",
                schema: "rapor",
                table: "vehicle_card",
                column: "normalized_plate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_visit_vehicle_card_id",
                schema: "rapor",
                table: "vehicle_visit",
                column: "vehicle_card_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admission_detail",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "audit_event",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "backup_log",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "custody_period",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "general_event_detail",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "handover_note_detail",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "inmate_file_history",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "material_detail",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "release_detail",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "report_entry_inmate",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "report_revision",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "report_snapshot",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "shift_duty",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "shift_gate",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "temporary_absence",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "transfer_detail",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "vehicle_card",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "vehicle_type",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "vehicle_visit",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "visitor_detail",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "report_entry",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "shift",
                schema: "rapor");

            migrationBuilder.DropTable(
                name: "inmate",
                schema: "rapor");

            migrationBuilder.DropSequence(
                name: "report_entry_number_seq",
                schema: "rapor");
        }
    }
}
