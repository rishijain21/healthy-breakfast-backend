# HealthyBreakfastApp Backend

A scalable **.NET 8 Clean Architecture backend** for a customizable healthy breakfast delivery platform that allows users to build personalized meals and schedule fresh breakfast deliveries.

The platform focuses on **nutrition, convenience, and personalization** for busy professionals and students by allowing them to customize ingredients such as oats, seeds, fruits, milk, and sweeteners.

The backend powers authentication, meal customization, order scheduling, and future AI-driven meal recommendations.

---

# Product Vision

Most breakfast delivery platforms offer **fixed meal options**.

HealthyBreakfastApp enables users to **create their own breakfast bowls** by choosing ingredients and nutritional preferences. The meal is prepared fresh and delivered the next morning.

### Key Goals

- Personalization of healthy meals  
- Convenience for busy professionals  
- Data-driven nutrition insights  
- Subscription-based breakfast delivery  
- AI-powered meal recommendations  

---

# Core Features

## User Authentication

- JWT based authentication
- Secure login and registration
- Role based access (future admin dashboard)

---

## Custom Meal Builder

Users can build meals by selecting ingredients such as:

- Oats type (rolled oats, steel cut oats, chocolate oats)
- Seeds (chia seeds, flax seeds, sunflower seeds)
- Dry fruits
- Fruits
- Milk type (toned milk, almond milk, soy milk)
- Natural sweeteners (honey, jaggery)

Each selection contributes to the **macro nutritional profile** of the meal.

---

## Macro Nutrition Calculator

The backend calculates nutritional values such as:

- Calories
- Protein
- Carbohydrates
- Fats

This helps users create meals aligned with their **fitness or dietary goals**.

---

## Meal Scheduling

Users can schedule breakfast delivery by selecting:

- Delivery date
- Delivery slot
- Custom meal configuration

Meals are prepared fresh and delivered the next morning.

---

## Order Management

Users can:

- View order history
- Track upcoming deliveries
- Manage scheduled meals

---

## AI Meal Recommendation (Planned)

Future integration with **Python microservices** will enable:

- Goal based meal suggestions
- Personalized nutrition insights
- Habit based meal recommendations

---

# Tech Stack

## Backend

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- Clean Architecture

---

## Database

- PostgreSQL

---

## Authentication

- JWT Tokens
- ASP.NET Identity (planned)

---

## Infrastructure

- Docker (planned)
- Azure deployment (planned)

---

## Frontend (separate repository)

- Angular 19
- Angular Material
- Tailwind CSS

---

## Future Services

- Python AI recommendation engine

---

# Architecture

This project follows **Clean Architecture principles** to ensure scalability, maintainability, and testability.

```
HealthyBreakfastApp
│
├── HealthyBreakfastApp.Domain
│   ├── Entities
│   └── Core business models
│
├── HealthyBreakfastApp.Application
│   ├── DTOs
│   ├── Interfaces
│   └── Business use cases
│
├── HealthyBreakfastApp.Infrastructure
│   ├── Database context
│   ├── Repository implementations
│   └── Services
│
├── HealthyBreakfastApp.WebAPI
│   ├── Controllers
│   ├── Middleware
│   └── API configuration
```

---

# Database Design

The database separates **static ingredient data** from **user generated data**.

## Static Tables (int ID)

- MealCategory
- MealItemOption

These tables store predefined ingredient options available in the system.

---

## User Tables (Guid ID)

- User
- CustomMeal
- CustomMealItem
- Order

These tables represent user created data.

---

## Entity Relationships

```
User
│
├── CustomMeal
│       │
│       └── CustomMealItem
│               │
│               └── MealItemOption
│
└── Order
```

---

# API Endpoints (Phase 1)

## Authentication

```
POST /api/auth/register
POST /api/auth/login
```

---

## Custom Meals

```
POST /api/custom-meals
GET /api/custom-meals
GET /api/custom-meals/{id}
```

---

## Orders

```
POST /api/orders
GET /api/orders
```

---

# Example Custom Meal Request

```json
{
  "mealName": "Protein Power Bowl",
  "items": [
    {
      "mealItemOptionId": 1,
      "quantity": 1
    },
    {
      "mealItemOptionId": 4,
      "quantity": 2
    }
  ]
}
```

---

# Local Development Setup

## 1. Clone Repository

```bash
git clone https://github.com/rishijain21/healthy-breakfast-backend.git
```

---

## 2. Navigate to Project

```bash
cd healthy-breakfast-backend
```

---

## 3. Setup Database

Install PostgreSQL and update the connection string inside:

`appsettings.json`

Example:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=HealthyBreakfastDB;Username=postgres;Password=yourpassword"
}
```

---

## 4. Apply Migrations

```bash
dotnet ef database update
```

---

## 5. Run Application

```bash
dotnet run
```

API will run at:

```
https://localhost:5001
```

Swagger documentation is available at:

```
/swagger
```

---

# Future Roadmap

## Phase 2

- Meal subscription plans
- Delivery scheduling optimization
- Admin dashboard

---

## Phase 3

- AI meal recommendation engine
- Nutrition analytics
- Habit tracking

---

## Phase 4

- Real time kitchen operations dashboard
- Vendor ingredient management
- Delivery partner system

---

# Scalability Considerations

The backend is designed to evolve into **microservices architecture**.

Possible service split:

```
Auth Service
Meal Builder Service
Order Service
Nutrition Engine
AI Recommendation Service
```

Services can communicate through **event driven architecture using message queues**.

---

# Security

- JWT authentication
- Input validation
- Role based authorization
- Secure password hashing

Future improvements include:

- Rate limiting
- API gateway
- OAuth integrations

---

# Contributing

Contributions are welcome.

Steps to contribute:

1. Fork the repository  
2. Create a feature branch  
3. Commit your changes  
4. Open a pull request  

---

# License

This project is licensed under the **MIT License**.

---

# Author

**Rishi Jain**

Software Engineer | .NET Full Stack Developer

Built as part of a scalable SaaS platform for healthy meal delivery.
