# WorkerTemplate

A basic C# Worker Service template designed for backend processing, with built-in support for common messaging, database, and caching technologies

This project provides a simple starting point for building background workers that consume messages, process data, and store results in a database.

> ⚠️ **Note:** This is a **template**, not a complete, ready-to-deploy application. It builds successfully out of the box, but anyone using it should modify the services and models according to their own project needs and requirements.

---

## Features

- ✅ RabbitMQ connection handling
- ✅ PostgreSQL connection handling
- ✅ Microsoft SQL Server connection handling
- ✅ Redis connection handling
- ✅ ElasticSearch API Request handling
- ✅ Basic publish and subscribe messaging
- ✅ Message consumption and data insertion into PostgreSQL
- ✅ HMAC SHA256 hashing utility for signature verification and authentication
- ✅ Clean structure suitable for extension into production services

---

## Use Case

This template is suitable for:

- Background processing services
- Message-driven architectures
- Queue-based data processing
- Integration services between systems

---

## Example Project Flow

1. Worker connects to RabbitMQ.
2. Messages are published or consumed from configured queues.
3. Incoming messages are processed.
4. Data is inserted into PostgreSQL.
5. Optional signature verification using HMAC SHA256.

---

## Requirements

- .NET (Worker Service)
- RabbitMQ
- PostgreSQL
- Microsoft SQL Server
- Redis

---

## Getting Started

1. Clone the repository

   ```bash
   git clone https://github.com/khairul006/WorkerTemplate.git
   ```

2. Configure database and RabbitMQ settings in `appsettings.json`

3. Run the worker service:

   ```bash
   dotnet run
   ```

---

## Register .NET Worker Template

After cloning or pulling this repository, register the template with your local .NET CLI.

1. Go to the template directory

   ```powershell
   cd WorkerTemplate
   ```

2. Register the template

   ```powershell
   dotnet new install . --force
   ```

   > Use `--force` to replace an existing installation with the latest version.

3. Verify the template

   ```powershell
   dotnet new list
   ```

   You should see:

   ```text
   Template Name          Short Name       Language  Tags
   ---------------------  ---------------  --------  ---------------
   TERAS Worker Template  WorkerTemplate   [C#]      Worker/Template
   ```

4. Create a new Worker Service

   ```powershell
   dotnet new WorkerTemplate -n MyWorker
   ```

The template is now registered and ready to use.

---

## Notes

This project is intended as a **starter template**, not a complete framework. It compiles and runs successfully as-is, but the services and models included are only examples.

Before using it in a real project, you should:

- Replace the sample services and models with ones that match your own business logic
- Remove any unnecessary or unused components
- Adjust configuration (PostgreSQL, RabbitMQ, Redis) to match your environment

The template is designed to be flexible and adaptable for various backend processing needs.