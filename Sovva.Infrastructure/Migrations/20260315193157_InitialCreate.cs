using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sovva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientCategories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientCategories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    MealId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MealName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ApproxCalories = table.Column<int>(type: "integer", nullable: true),
                    ApproxProtein = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    ApproxCarbs = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    ApproxFats = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.MealId);
                });

            migrationBuilder.CreateTable(
                name: "ServiceableLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Locality = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LandmarkOrSociety = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    DeliveryTimeSlot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceableLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "text", nullable: true),
                    AccountStatus = table.Column<string>(type: "text", nullable: false),
                    WalletBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    IngredientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    IngredientName = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Available = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Calories = table.Column<int>(type: "integer", nullable: false),
                    Protein = table.Column<decimal>(type: "numeric", nullable: false),
                    Fiber = table.Column<decimal>(type: "numeric", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IconEmoji = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.IngredientId);
                    table.ForeignKey(
                        name: "FK_Ingredients_IngredientCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IngredientCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealOptions",
                columns: table => new
                {
                    MealOptionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MealId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MaxSelectable = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealOptions", x => x.MealOptionId);
                    table.ForeignKey(
                        name: "FK_MealOptions_IngredientCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IngredientCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealOptions_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "MealId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_auth_mapping",
                columns: table => new
                {
                    mapping_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    auth_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_auth_mapping", x => x.mapping_id);
                    table.ForeignKey(
                        name: "FK_user_auth_mapping_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAddresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ServiceableLocationId = table.Column<int>(type: "integer", nullable: false),
                    Wing = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Block = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FlatNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Floor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AdditionalInstructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAddresses_ServiceableLocations_ServiceableLocationId",
                        column: x => x.ServiceableLocationId,
                        principalTable: "ServiceableLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAddresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMeals",
                columns: table => new
                {
                    UserMealId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MealId = table.Column<int>(type: "integer", nullable: false),
                    MealName = table.Column<string>(type: "text", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMeals", x => x.UserMealId);
                    table.ForeignKey(
                        name: "FK_UserMeals_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "MealId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMeals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealOptionIngredients",
                columns: table => new
                {
                    MealOptionIngredientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MealOptionId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealOptionIngredients", x => x.MealOptionIngredientId);
                    table.ForeignKey(
                        name: "FK_MealOptionIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealOptionIngredients_MealOptions_MealOptionId",
                        column: x => x.MealOptionId,
                        principalTable: "MealOptions",
                        principalColumn: "MealOptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserMealId = table.Column<int>(type: "integer", nullable: false),
                    DeliveryAddressId = table.Column<int>(type: "integer", nullable: true),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    NextScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.SubscriptionId);
                    table.ForeignKey(
                        name: "FK_Subscriptions_UserAddresses_DeliveryAddressId",
                        column: x => x.DeliveryAddressId,
                        principalTable: "UserAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Subscriptions_UserMeals_UserMealId",
                        column: x => x.UserMealId,
                        principalTable: "UserMeals",
                        principalColumn: "UserMealId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMealIngredients",
                columns: table => new
                {
                    UserMealIngredientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserMealId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMealIngredients", x => x.UserMealIngredientId);
                    table.ForeignKey(
                        name: "FK_UserMealIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMealIngredients_UserMeals_UserMealId",
                        column: x => x.UserMealId,
                        principalTable: "UserMeals",
                        principalColumn: "UserMealId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledOrders",
                columns: table => new
                {
                    ScheduledOrderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AuthId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MealId = table.Column<int>(type: "integer", nullable: true),
                    MealImageUrl = table.Column<string>(type: "text", nullable: true),
                    ScheduledFor = table.Column<DateTime>(type: "date", nullable: false),
                    DeliveryTimeSlot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    NutritionalSummary = table.Column<string>(type: "text", nullable: true),
                    OrderStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CanModify = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsProcessedToOrder = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConfirmedOrderId = table.Column<int>(type: "integer", nullable: true),
                    DeliveryAddressId = table.Column<int>(type: "integer", nullable: true),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledOrders", x => x.ScheduledOrderId);
                    table.ForeignKey(
                        name: "FK_ScheduledOrders_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "SubscriptionId");
                    table.ForeignKey(
                        name: "FK_ScheduledOrders_UserAddresses_DeliveryAddressId",
                        column: x => x.DeliveryAddressId,
                        principalTable: "UserAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduledOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionSchedules",
                columns: table => new
                {
                    ScheduleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false, comment: "Day of week: 0=Sunday, 1=Monday, 2=Tuesday, 3=Wednesday, 4=Thursday, 5=Friday, 6=Saturday"),
                    Quantity = table.Column<int>(type: "integer", nullable: false, comment: "Number of items to deliver on this day"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionSchedules", x => x.ScheduleId);
                    table.CheckConstraint("CK_SubscriptionSchedules_DayOfWeek", "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6");
                    table.CheckConstraint("CK_SubscriptionSchedules_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SubscriptionSchedules_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "SubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserMealId = table.Column<int>(type: "integer", nullable: true),
                    ScheduledOrderId = table.Column<int>(type: "integer", nullable: true),
                    DeliveryAddressId = table.Column<int>(type: "integer", nullable: true),
                    IsPrepared = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_Orders_ScheduledOrders_ScheduledOrderId",
                        column: x => x.ScheduledOrderId,
                        principalTable: "ScheduledOrders",
                        principalColumn: "ScheduledOrderId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_UserAddresses_DeliveryAddressId",
                        column: x => x.DeliveryAddressId,
                        principalTable: "UserAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_UserMeals_UserMealId",
                        column: x => x.UserMealId,
                        principalTable: "UserMeals",
                        principalColumn: "UserMealId");
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledOrderIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduledOrderId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledOrderIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledOrderIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledOrderIngredients_ScheduledOrders_ScheduledOrderId",
                        column: x => x.ScheduledOrderId,
                        principalTable: "ScheduledOrders",
                        principalColumn: "ScheduledOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    OrderItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    UserMealId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.OrderItemId);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_UserMeals_UserMealId",
                        column: x => x.UserMealId,
                        principalTable: "UserMeals",
                        principalColumn: "UserMealId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "IngredientCategories",
                columns: new[] { "CategoryId", "CategoryName", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Oats", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Seeds", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "Fruits", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "Milk", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "Sweetener", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Meals",
                columns: new[] { "MealId", "ApproxCalories", "ApproxCarbs", "ApproxFats", "ApproxProtein", "BasePrice", "CreatedAt", "Description", "ImageUrl", "IsComplete", "IsDeleted", "MealName", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, null, null, null, null, 40m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Traditional overnight oats base", null, true, false, "Classic Overnight Oats", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, null, null, null, null, 50m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Build your perfect breakfast", null, true, false, "Custom Breakfast Bowl", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, null, null, null, null, 60m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "High protein breakfast option", null, true, false, "Protein Power Bowl", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "AccountStatus", "CreatedAt", "DeliveryAddress", "Email", "Name", "Phone", "Role", "UpdatedAt", "WalletBalance" },
                values: new object[] { 1, "Active", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "testuser@healthybreakfast.com", "TestUser", "1234567890", "User", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 625m });

            migrationBuilder.InsertData(
                table: "WalletTransactions",
                columns: new[] { "TransactionId", "Amount", "CreatedAt", "Description", "Type", "UserId" },
                values: new object[] { 1, 625m, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Initial wallet balance", "Credit", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_CategoryId",
                table: "Ingredients",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MealOptionIngredients_IngredientId",
                table: "MealOptionIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_MealOptionIngredients_MealOptionId",
                table: "MealOptionIngredients",
                column: "MealOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_MealOptions_CategoryId",
                table: "MealOptions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MealOptions_MealId",
                table: "MealOptions",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_UserMealId",
                table: "OrderItems",
                column: "UserMealId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryAddressId",
                table: "Orders",
                column: "DeliveryAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ScheduledFor",
                table: "Orders",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ScheduledOrderId",
                table: "Orders",
                column: "ScheduledOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_Status",
                table: "Orders",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserMealId",
                table: "Orders",
                column: "UserMealId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrderIngredients_IngredientId",
                table: "ScheduledOrderIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrderIngredients_ScheduledOrderId",
                table: "ScheduledOrderIngredients",
                column: "ScheduledOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_AuthId_ScheduledFor",
                table: "ScheduledOrders",
                columns: new[] { "AuthId", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_DeliveryAddressId",
                table: "ScheduledOrders",
                column: "DeliveryAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_ScheduledFor",
                table: "ScheduledOrders",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_ScheduledFor_OrderStatus",
                table: "ScheduledOrders",
                columns: new[] { "ScheduledFor", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_SubscriptionId",
                table: "ScheduledOrders",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOrders_UserId",
                table: "ScheduledOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceableLocations_City_Area",
                table: "ServiceableLocations",
                columns: new[] { "City", "Area" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceableLocations_Pincode",
                table: "ServiceableLocations",
                column: "Pincode");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Active_StartDate_EndDate",
                table: "Subscriptions",
                columns: new[] { "Active", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_DeliveryAddressId",
                table: "Subscriptions",
                column: "DeliveryAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId_UserMealId",
                table: "Subscriptions",
                columns: new[] { "UserId", "UserMealId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserMealId",
                table: "Subscriptions",
                column: "UserMealId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionSchedules_Subscription_DayOfWeek",
                table: "SubscriptionSchedules",
                columns: new[] { "SubscriptionId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_auth_mapping_auth_id",
                table: "user_auth_mapping",
                column: "auth_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_auth_mapping_user_id",
                table: "user_auth_mapping",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_Primary_Unique",
                table: "UserAddresses",
                columns: new[] { "UserId", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true AND \"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_ServiceableLocationId",
                table: "UserAddresses",
                column: "ServiceableLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_UserId",
                table: "UserAddresses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMealIngredients_IngredientId",
                table: "UserMealIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMealIngredients_UserMealId",
                table: "UserMealIngredients",
                column: "UserMealId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMeals_MealId",
                table: "UserMeals",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMeals_UserId",
                table: "UserMeals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserId",
                table: "WalletTransactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealOptionIngredients");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "ScheduledOrderIngredients");

            migrationBuilder.DropTable(
                name: "SubscriptionSchedules");

            migrationBuilder.DropTable(
                name: "user_auth_mapping");

            migrationBuilder.DropTable(
                name: "UserMealIngredients");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "MealOptions");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "ScheduledOrders");

            migrationBuilder.DropTable(
                name: "IngredientCategories");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "UserAddresses");

            migrationBuilder.DropTable(
                name: "UserMeals");

            migrationBuilder.DropTable(
                name: "ServiceableLocations");

            migrationBuilder.DropTable(
                name: "Meals");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
