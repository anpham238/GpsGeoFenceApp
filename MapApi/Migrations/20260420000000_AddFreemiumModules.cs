using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapApi.Migrations
{
    public partial class AddFreemiumModules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DailyUsageTracking
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DailyUsageTracking')
BEGIN
    CREATE TABLE [dbo].[DailyUsageTracking] (
        [EntityId]    varchar(100)   NOT NULL,
        [ActionType]  varchar(20)    NOT NULL,
        [UsedCount]   int            NOT NULL DEFAULT 0,
        [LastResetAt] datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_DailyUsageTracking] PRIMARY KEY CLUSTERED ([EntityId] ASC, [ActionType] ASC)
    )
END");

            // SupportedLanguages
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SupportedLanguages')
BEGIN
    CREATE TABLE [dbo].[SupportedLanguages] (
        [LanguageTag]  nvarchar(10)  NOT NULL,
        [LanguageName] nvarchar(50)  NOT NULL,
        [IsPremium]    bit           NOT NULL DEFAULT 0,
        [IsActive]     bit           NOT NULL DEFAULT 1,
        CONSTRAINT [PK_SupportedLanguages] PRIMARY KEY CLUSTERED ([LanguageTag] ASC)
    )
END");

            // Seed SupportedLanguages
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[SupportedLanguages] WHERE [LanguageTag] = 'vi-VN')
BEGIN
    INSERT INTO [dbo].[SupportedLanguages] ([LanguageTag],[LanguageName],[IsPremium],[IsActive]) VALUES
        ('vi-VN', N'Tiếng Việt',   0, 1),
        ('en-US', N'English',      0, 1),
        ('zh-Hans',N'中文 (简体)',  1, 1),
        ('ja-JP', N'日本語',       1, 1),
        ('ko-KR', N'한국어',       1, 1),
        ('de-DE', N'Deutsch',      1, 1)
END");

            // ProPodcastScript column on PoiLanguage
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PoiLanguage' AND COLUMN_NAME = 'ProPodcastScript')
BEGIN
    ALTER TABLE [dbo].[PoiLanguage] ADD [ProPodcastScript] nvarchar(max) NULL
END");

            // ProAudioUrl column on PoiLanguage
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PoiLanguage' AND COLUMN_NAME = 'ProAudioUrl')
BEGIN
    ALTER TABLE [dbo].[PoiLanguage] ADD [ProAudioUrl] nvarchar(1000) NULL
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[DailyUsageTracking]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[SupportedLanguages]");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PoiLanguage' AND COLUMN_NAME='ProAudioUrl')
    ALTER TABLE [dbo].[PoiLanguage] DROP COLUMN [ProAudioUrl]");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PoiLanguage' AND COLUMN_NAME='ProPodcastScript')
    ALTER TABLE [dbo].[PoiLanguage] DROP COLUMN [ProPodcastScript]");
        }
    }
}
