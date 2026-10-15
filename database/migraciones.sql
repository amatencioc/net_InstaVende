IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(max) NOT NULL,
        [LastName] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [Businesses] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [LogoUrl] nvarchar(max) NULL,
        [WhatsAppNumber] nvarchar(max) NULL,
        [FacebookPageId] nvarchar(max) NULL,
        [InstagramAccountId] nvarchar(max) NULL,
        [WebsiteUrl] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [BusinessSector] nvarchar(max) NULL,
        [SocialMedia] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Businesses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Businesses_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [BotConfigs] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [BotName] nvarchar(max) NOT NULL,
        [Personality] nvarchar(max) NULL,
        [BaseSystemPrompt] nvarchar(max) NULL,
        [InteractionLevel] int NOT NULL,
        [Language] nvarchar(max) NOT NULL,
        [FallbackMessage] nvarchar(max) NOT NULL,
        [EnableHandoff] bit NOT NULL,
        [HandoffTriggerPhrase] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [DiscountLevel1] decimal(5,2) NOT NULL,
        [DiscountLevel2] decimal(5,2) NOT NULL,
        [DiscountLevel3] decimal(5,2) NOT NULL,
        [MaxDiscountPercent] decimal(5,2) NOT NULL,
        [MinMarginPercent] decimal(5,2) NOT NULL,
        [FreeShippingThreshold] decimal(10,2) NOT NULL,
        [BundleDiscount] decimal(5,2) NOT NULL,
        [LoyaltyDiscount] decimal(5,2) NOT NULL,
        [BusinessHours] nvarchar(max) NULL,
        [ReturnPolicy] nvarchar(max) NULL,
        [WarrantyPolicy] nvarchar(max) NULL,
        [ShippingTimes] nvarchar(max) NULL,
        [PaymentMethods] nvarchar(max) NULL,
        [ActivePromotions] nvarchar(max) NULL,
        CONSTRAINT [PK_BotConfigs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BotConfigs_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [ChannelConfigs] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ChannelType] int NOT NULL,
        [PhoneNumberId] nvarchar(max) NULL,
        [PageId] nvarchar(max) NULL,
        [InstagramAccountId] nvarchar(max) NULL,
        [AccessTokenEncrypted] nvarchar(max) NOT NULL,
        [AppSecretEncrypted] nvarchar(max) NULL,
        [WebhookVerifyToken] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChannelConfigs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChannelConfigs_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [Contacts] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ChannelType] int NOT NULL,
        [ExternalId] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [AvatarUrl] nvarchar(max) NULL,
        [FirstSeenAt] datetime2 NOT NULL,
        [LastSeenAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Contacts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Contacts_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductCategories_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [BotIntents] (
        [Id] int NOT NULL IDENTITY,
        [BotConfigId] int NOT NULL,
        [IntentName] nvarchar(max) NOT NULL,
        [TriggerPhrases] nvarchar(max) NOT NULL,
        [Response] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [Priority] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BotIntents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BotIntents_BotConfigs_BotConfigId] FOREIGN KEY ([BotConfigId]) REFERENCES [BotConfigs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [BotKnowledges] (
        [Id] int NOT NULL IDENTITY,
        [BotConfigId] int NOT NULL,
        [Question] nvarchar(max) NOT NULL,
        [Answer] nvarchar(max) NOT NULL,
        [Category] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BotKnowledges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BotKnowledges_BotConfigs_BotConfigId] FOREIGN KEY ([BotConfigId]) REFERENCES [BotConfigs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [ConversationFlows] (
        [Id] int NOT NULL IDENTITY,
        [BotConfigId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [StepsJson] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [IsEntryFlow] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ConversationFlows] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConversationFlows_BotConfigs_BotConfigId] FOREIGN KEY ([BotConfigId]) REFERENCES [BotConfigs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [Conversations] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ContactId] int NOT NULL,
        [ChannelType] int NOT NULL,
        [Status] int NOT NULL,
        [AssignedAgentId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversations_AspNetUsers_AssignedAgentId] FOREIGN KEY ([AssignedAgentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Conversations_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]),
        CONSTRAINT [FK_Conversations_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [CategoryId] int NULL,
        [Name] nvarchar(300) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Price] decimal(18,2) NOT NULL,
        [Stock] int NOT NULL,
        [ImageUrl] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [IsFeatured] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Products_ProductCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ProductCategories] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] int NOT NULL IDENTITY,
        [ConversationId] int NOT NULL,
        [Direction] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [MediaUrl] nvarchar(max) NULL,
        [MediaType] nvarchar(max) NULL,
        [ExternalMessageId] nvarchar(max) NULL,
        [IsRead] bit NOT NULL,
        [SentByBot] bit NOT NULL,
        [SentAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BotConfigs_BusinessId] ON [BotConfigs] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BotIntents_BotConfigId] ON [BotIntents] ([BotConfigId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BotKnowledges_BotConfigId] ON [BotKnowledges] ([BotConfigId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Businesses_UserId] ON [Businesses] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelConfigs_BusinessId_ChannelType] ON [ChannelConfigs] ([BusinessId], [ChannelType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Contacts_BusinessId_ChannelType_ExternalId] ON [Contacts] ([BusinessId], [ChannelType], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ConversationFlows_BotConfigId] ON [ConversationFlows] ([BotConfigId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_AssignedAgentId] ON [Conversations] ([AssignedAgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_BusinessId] ON [Conversations] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_ContactId] ON [Conversations] ([ContactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_ConversationId] ON [Messages] ([ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductCategories_BusinessId] ON [ProductCategories] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_BusinessId] ON [Products] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419185904_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419185904_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Products] ADD [PriceOriginal] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Products] ADD [Sku] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Products] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Conversations] ADD [LastMessagePreview] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Conversations] ADD [UnreadCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Contacts] ADD [LastPurchaseAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Contacts] ADD [TotalPurchases] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Contacts] ADD [TotalSpent] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Businesses] ADD [CompletionPercent] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [Businesses] ADD [Sector] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [AvatarUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LastLoginAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE TABLE [OnboardingProgresses] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ChannelConnected] bit NOT NULL,
        [ProductsAdded] bit NOT NULL,
        [PaymentMethodsAdded] bit NOT NULL,
        [BotConfigured] bit NOT NULL,
        [BotTested] bit NOT NULL,
        [OnboardingCompleted] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OnboardingProgresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OnboardingProgresses_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ContactId] int NOT NULL,
        [ConversationId] int NULL,
        [OrderNumber] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [Discount] decimal(18,2) NOT NULL,
        [ShippingCost] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [PaymentMethodName] nvarchar(max) NULL,
        [ShippingAddress] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [ChannelType] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]),
        CONSTRAINT [FK_Orders_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE TABLE [PaymentMethods] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Type] int NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Instructions] nvarchar(max) NULL,
        [AccountAlias] nvarchar(max) NULL,
        [AccountNumber] nvarchar(max) NULL,
        [PaymentLink] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentMethods] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentMethods_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [Url] nvarchar(max) NOT NULL,
        [IsMain] bit NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [AttributeName] nvarchar(max) NOT NULL,
        [AttributeValue] nvarchar(max) NOT NULL,
        [PriceModifier] decimal(18,2) NOT NULL,
        [Stock] int NOT NULL,
        [Sku] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE TABLE [Reminders] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ContactId] int NOT NULL,
        [ConversationId] int NULL,
        [Message] nvarchar(max) NOT NULL,
        [ChannelType] int NOT NULL,
        [Status] int NOT NULL,
        [ScheduledAt] datetime2 NOT NULL,
        [SentAt] datetime2 NULL,
        [TemplateKey] nvarchar(max) NULL,
        [CreatedByAgentId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Reminders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reminders_AspNetUsers_CreatedByAgentId] FOREIGN KEY ([CreatedByAgentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Reminders_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]),
        CONSTRAINT [FK_Reminders_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductId] int NOT NULL,
        [ProductName] nvarchar(max) NOT NULL,
        [VariantDescription] nvarchar(max) NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OnboardingProgresses_BusinessId] ON [OnboardingProgresses] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_Orders_BusinessId] ON [Orders] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_Orders_ContactId] ON [Orders] ([ContactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_Orders_ConversationId] ON [Orders] ([ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_PaymentMethods_BusinessId] ON [PaymentMethods] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_Reminders_BusinessId] ON [Reminders] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_Reminders_ContactId] ON [Reminders] ([ContactId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    CREATE INDEX [IX_Reminders_CreatedByAgentId] ON [Reminders] ([CreatedByAgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419232744_AddPhase2Modules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419232744_AddPhase2Modules', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [Orders] ADD [SubStatus] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [Conversations] ADD [LabelId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [Conversations] ADD [LastMessageAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [ChannelConfigs] ADD [ConnectedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [ChannelConfigs] ADD [PhoneNumber] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [ChannelConfigs] ADD [WabaId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [Businesses] ADD [Country] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [Businesses] ADD [Currency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [BusinessUsers] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Role] int NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessUsers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessUsers_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [ConversationLabels] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Color] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ConversationLabels] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConversationLabels_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [DeliveryZones] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Cost] decimal(18,2) NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryZones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryZones_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [KnowledgeEntries] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Category] int NOT NULL,
        [IsFavorite] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_KnowledgeEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KnowledgeEntries_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [NotificationEmails] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_NotificationEmails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationEmails_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [PaymentImages] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ImageUrl] nvarchar(max) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentImages_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [ReminderTemplates] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Segment] int NOT NULL,
        [Order] int NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [TimeWindow] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [MediaUrl] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ReminderTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReminderTemplates_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [UserInvitations] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Token] nvarchar(450) NOT NULL,
        [Role] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserInvitations_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE TABLE [VendorConfigs] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [VendorName] nvarchar(max) NOT NULL,
        [VendorGender] nvarchar(max) NULL,
        [Country] nvarchar(max) NULL,
        [BusinessDescription] nvarchar(max) NULL,
        [TargetAudience] nvarchar(max) NULL,
        [Rules] nvarchar(max) NULL,
        [CommunicationStyle] nvarchar(max) NULL,
        [SalesStyle] nvarchar(max) NULL,
        [ResponseLength] int NOT NULL,
        [UseEmojis] bit NOT NULL,
        [UseOpeningPunctuation] bit NOT NULL,
        [WordsToAvoid] nvarchar(max) NULL,
        [EmojiPalette] nvarchar(max) NULL,
        [WelcomeMessage] nvarchar(max) NULL,
        [WelcomeMediaUrl] nvarchar(max) NULL,
        [PurchaseConfirmationMessage] nvarchar(max) NULL,
        [HumanHandoffSituations] nvarchar(max) NULL,
        [AutoPauseOnHandoff] bit NOT NULL,
        [HandoffExampleMessage] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorConfigs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorConfigs_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_Conversations_LabelId] ON [Conversations] ([LabelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_BusinessUsers_BusinessId] ON [BusinessUsers] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_BusinessUsers_UserId] ON [BusinessUsers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_ConversationLabels_BusinessId] ON [ConversationLabels] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_DeliveryZones_BusinessId] ON [DeliveryZones] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_KnowledgeEntries_BusinessId] ON [KnowledgeEntries] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_NotificationEmails_BusinessId] ON [NotificationEmails] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_PaymentImages_BusinessId] ON [PaymentImages] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_ReminderTemplates_BusinessId] ON [ReminderTemplates] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE INDEX [IX_UserInvitations_BusinessId] ON [UserInvitations] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserInvitations_Token] ON [UserInvitations] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorConfigs_BusinessId] ON [VendorConfigs] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    ALTER TABLE [Conversations] ADD CONSTRAINT [FK_Conversations_ConversationLabels_LabelId] FOREIGN KEY ([LabelId]) REFERENCES [ConversationLabels] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421050348_AddPhase3Modules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260421050348_AddPhase3Modules', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DROP INDEX [IX_ReminderTemplates_BusinessId] ON [ReminderTemplates];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DROP INDEX [IX_Reminders_BusinessId] ON [Reminders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DROP INDEX [IX_Orders_BusinessId] ON [Orders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DROP INDEX [IX_Orders_ConversationId] ON [Orders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DROP INDEX [IX_Conversations_BusinessId] ON [Conversations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DROP INDEX [IX_Conversations_ContactId] ON [Conversations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductCategories]') AND [c].[name] = N'Name');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [ProductCategories] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [ProductCategories] ALTER COLUMN [Name] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConversationLabels]') AND [c].[name] = N'Name');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ConversationLabels] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [ConversationLabels] ALTER COLUMN [Name] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BotIntents]') AND [c].[name] = N'IntentName');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [BotIntents] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [BotIntents] ALTER COLUMN [IntentName] nvarchar(200) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_ReminderTemplates_BusinessId_Segment] ON [ReminderTemplates] ([BusinessId], [Segment]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Reminders_BusinessId_Status] ON [Reminders] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Reminders_ScheduledAt] ON [Reminders] ([ScheduledAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Orders_BusinessId_CreatedAt_Status] ON [Orders] ([BusinessId], [CreatedAt], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Orders_BusinessId_Status] ON [Orders] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Orders_ConversationId_Status] ON [Orders] ([ConversationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Messages_ConversationId_SentAt] ON [Messages] ([ConversationId], [SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Conversations_BusinessId_Status] ON [Conversations] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    CREATE INDEX [IX_Conversations_ContactId_Status] ON [Conversations] ([ContactId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053513_AddOrderBusinessCreatedAtStatusIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518053513_AddOrderBusinessCreatedAtStatusIndex', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053743_AddConversationBusinessCreatedAtIndex'
)
BEGIN
    CREATE INDEX [IX_Conversations_BusinessId_CreatedAt] ON [Conversations] ([BusinessId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518053743_AddConversationBusinessCreatedAtIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518053743_AddConversationBusinessCreatedAtIndex', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054120_AddConversationBusinessUpdatedAtIndex'
)
BEGIN
    CREATE INDEX [IX_Conversations_BusinessId_UpdatedAt] ON [Conversations] ([BusinessId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054120_AddConversationBusinessUpdatedAtIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518054120_AddConversationBusinessUpdatedAtIndex', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054423_AddProductBotQueryIndexes'
)
BEGIN
    DROP INDEX [IX_Products_BusinessId] ON [Products];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054423_AddProductBotQueryIndexes'
)
BEGIN
    CREATE INDEX [IX_Products_BusinessId_IsActive_CategoryId] ON [Products] ([BusinessId], [IsActive], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054423_AddProductBotQueryIndexes'
)
BEGIN
    CREATE INDEX [IX_Products_BusinessId_IsActive_SortOrder] ON [Products] ([BusinessId], [IsActive], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054423_AddProductBotQueryIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518054423_AddProductBotQueryIndexes', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054623_AddProductAdminQueryIndexes'
)
BEGIN
    CREATE INDEX [IX_Products_BusinessId_CreatedAt] ON [Products] ([BusinessId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054623_AddProductAdminQueryIndexes'
)
BEGIN
    CREATE INDEX [IX_Products_BusinessId_IsActive_Name] ON [Products] ([BusinessId], [IsActive], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518054623_AddProductAdminQueryIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518054623_AddProductAdminQueryIndexes', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055024_AddVendedorIndexes'
)
BEGIN
    DROP INDEX [IX_KnowledgeEntries_BusinessId] ON [KnowledgeEntries];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055024_AddVendedorIndexes'
)
BEGIN
    DROP INDEX [IX_DeliveryZones_BusinessId] ON [DeliveryZones];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055024_AddVendedorIndexes'
)
BEGIN
    CREATE INDEX [IX_KnowledgeEntries_BusinessId_IsFavorite_CreatedAt] ON [KnowledgeEntries] ([BusinessId], [IsFavorite], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055024_AddVendedorIndexes'
)
BEGIN
    CREATE INDEX [IX_DeliveryZones_BusinessId_SortOrder] ON [DeliveryZones] ([BusinessId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055024_AddVendedorIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518055024_AddVendedorIndexes', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055133_AddContactLastSeenAtIndex'
)
BEGIN
    CREATE INDEX [IX_Contacts_BusinessId_LastSeenAt] ON [Contacts] ([BusinessId], [LastSeenAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055133_AddContactLastSeenAtIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518055133_AddContactLastSeenAtIndex', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055712_AddConversationBusinessStatusUpdatedAtIndex'
)
BEGIN
    CREATE INDEX [IX_Conversations_BusinessId_Status_UpdatedAt] ON [Conversations] ([BusinessId], [Status], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518055712_AddConversationBusinessStatusUpdatedAtIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518055712_AddConversationBusinessStatusUpdatedAtIndex', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518060934_AddAccountConfigIndexes'
)
BEGIN
    DROP INDEX [IX_UserInvitations_BusinessId] ON [UserInvitations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518060934_AddAccountConfigIndexes'
)
BEGIN
    DROP INDEX [IX_PaymentMethods_BusinessId] ON [PaymentMethods];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518060934_AddAccountConfigIndexes'
)
BEGIN
    CREATE INDEX [IX_UserInvitations_BusinessId_Status] ON [UserInvitations] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518060934_AddAccountConfigIndexes'
)
BEGIN
    CREATE INDEX [IX_PaymentMethods_BusinessId_SortOrder] ON [PaymentMethods] ([BusinessId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518060934_AddAccountConfigIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518060934_AddAccountConfigIndexes', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DROP INDEX [IX_Reminders_BusinessId_Status] ON [Reminders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DROP INDEX [IX_Reminders_ScheduledAt] ON [Reminders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DROP INDEX [IX_Messages_ConversationId] ON [Messages];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DROP INDEX [IX_Conversations_BusinessId_Status] ON [Conversations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DROP INDEX [IX_BusinessUsers_BusinessId] ON [BusinessUsers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    EXEC(N'UPDATE [Businesses] SET [Sector] = [BusinessSector] WHERE [Sector] IS NULL AND [BusinessSector] IS NOT NULL;')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Businesses]') AND [c].[name] = N'BusinessSector');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Businesses] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Businesses] DROP COLUMN [BusinessSector];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    EXEC(N'UPDATE [Reminders] SET [Message] = LEFT([Message], 1000) WHERE LEN([Message]) > 1000;')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    EXEC(N'UPDATE [Messages] SET [MediaType] = LEFT([MediaType], 50) WHERE LEN([MediaType]) > 50;')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reminders]') AND [c].[name] = N'Message');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Reminders] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [Reminders] ALTER COLUMN [Message] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Messages]') AND [c].[name] = N'MediaType');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Messages] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [Messages] ALTER COLUMN [MediaType] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Messages]') AND [c].[name] = N'ExternalMessageId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Messages] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [Messages] ALTER COLUMN [ExternalMessageId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    ALTER TABLE [Messages] ADD [DeliveryStatus] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    CREATE TABLE [DataProtectionKeys] (
        [Id] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(max) NULL,
        [Xml] nvarchar(max) NULL,
        CONSTRAINT [PK_DataProtectionKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    CREATE INDEX [IX_Reminders_BusinessId_ScheduledAt] ON [Reminders] ([BusinessId], [ScheduledAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    CREATE INDEX [IX_Reminders_Status_ScheduledAt] ON [Reminders] ([Status], [ScheduledAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    CREATE INDEX [IX_Orders_BusinessId_OrderNumber] ON [Orders] ([BusinessId], [OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    CREATE INDEX [IX_Orders_BusinessId_SubStatus] ON [Orders] ([BusinessId], [SubStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Messages_ExternalMessageId] ON [Messages] ([ExternalMessageId]) WHERE [ExternalMessageId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessUsers_BusinessId_UserId] ON [BusinessUsers] ([BusinessId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014234224_PlatformHardening'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014234224_PlatformHardening', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015002129_ConversationLastMessageIndex'
)
BEGIN
    CREATE INDEX [IX_Conversations_BusinessId_LastMessageAt] ON [Conversations] ([BusinessId], [LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015002129_ConversationLastMessageIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015002129_ConversationLastMessageIndex', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015003652_ScopeMessageIdempotencyToConversation'
)
BEGIN
    DROP INDEX [IX_Messages_ExternalMessageId] ON [Messages];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015003652_ScopeMessageIdempotencyToConversation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Messages_ConversationId_ExternalMessageId] ON [Messages] ([ConversationId], [ExternalMessageId]) WHERE [ExternalMessageId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015003652_ScopeMessageIdempotencyToConversation'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Messages_ExternalMessageId] ON [Messages] ([ExternalMessageId]) WHERE [ExternalMessageId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015003652_ScopeMessageIdempotencyToConversation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015003652_ScopeMessageIdempotencyToConversation', N'10.0.13');
END;

COMMIT;
GO

