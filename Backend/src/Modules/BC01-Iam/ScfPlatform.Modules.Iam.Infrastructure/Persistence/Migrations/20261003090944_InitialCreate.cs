using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "iam");

            migrationBuilder.CreateTable(
                name: "AdminActionLog",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActionLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeactivationCascadeRuns",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReceivedSignals = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeactivationCascadeRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "iam",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "OtpChallenges",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    IssuedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevokedTokenRecords",
                schema: "iam",
                columns: table => new
                {
                    TokenIdOrRefreshHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RevokedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevokedTokenRecords", x => x.TokenIdOrRefreshHash);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordAlgorithm = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    FailedOtpCount = table.Column<int>(type: "int", nullable: false),
                    Mfa_Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Mfa_Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mfa_SecretRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordHistory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Permissions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentityVerified = table.Column<bool>(type: "bit", nullable: false),
                    ActivatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeactivatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupCodes",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupCodes_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "iam",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "iam",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "iam",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrustedDevices",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrustedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustedDevices_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "iam",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActionLog_AdminUserId",
                schema: "iam",
                table: "AdminActionLog",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActionLog_OccurredOnUtc",
                schema: "iam",
                table: "AdminActionLog",
                column: "OccurredOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActionLog_TargetUserId",
                schema: "iam",
                table: "AdminActionLog",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupCodes_UserAccountId",
                schema: "iam",
                table: "BackupCodes",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DeactivationCascadeRuns_Status",
                schema: "iam",
                table: "DeactivationCascadeRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DeactivationCascadeRuns_UserId",
                schema: "iam",
                table: "DeactivationCascadeRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_UserAccountId_Purpose",
                schema: "iam",
                table: "OtpChallenges",
                columns: new[] { "UserAccountId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "ProcessedOnUtc",
                schema: "iam",
                table: "OutboxMessages",
                column: "ProcessedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                schema: "iam",
                table: "PasswordResetTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserAccountId",
                schema: "iam",
                table: "PasswordResetTokens",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_RefreshTokenHash",
                schema: "iam",
                table: "Sessions",
                column: "RefreshTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserAccountId",
                schema: "iam",
                table: "Sessions",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_UserAccountId",
                schema: "iam",
                table: "TrustedDevices",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Email",
                schema: "iam",
                table: "UserAccounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Mobile",
                schema: "iam",
                table: "UserAccounts",
                column: "Mobile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Role",
                schema: "iam",
                table: "UserAccounts",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Status",
                schema: "iam",
                table: "UserAccounts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminActionLog",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "BackupCodes",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "DeactivationCascadeRuns",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "OtpChallenges",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "RevokedTokenRecords",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "TrustedDevices",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "UserAccounts",
                schema: "iam");
        }
    }
}
