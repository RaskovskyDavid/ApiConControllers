using System;
using ApiConControllers.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace ApiConControllers.Data;
// IdentityDbContext brings in all the Identity tables (users, roles, claims, etc.).
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options);