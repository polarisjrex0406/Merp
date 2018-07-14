﻿// <auto-generated />
using System;
using Merp.ProjectManagement.QueryStack;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Merp.ProjectManagement.QueryStack.Migrations
{
    [DbContext(typeof(ProjectManagementDbContext))]
    [Migration("20180714124859_Base")]
    partial class Base
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.1.0-rtm-30799")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Merp.ProjectManagement.QueryStack.Model.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid?>("ContactPersonId");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasMaxLength(3);

                    b.Property<Guid>("CustomerId");

                    b.Property<string>("CustomerPurchaseOrderNumber");

                    b.Property<DateTime?>("DateOfCompletion");

                    b.Property<DateTime>("DateOfRegistration");

                    b.Property<DateTime?>("DateOfStart");

                    b.Property<string>("Description");

                    b.Property<DateTime?>("DueDate");

                    b.Property<bool>("IsCompleted");

                    b.Property<bool>("IsTimeAndMaterial");

                    b.Property<Guid>("ManagerId");

                    b.Property<string>("Name")
                        .IsRequired();

                    b.Property<string>("Number")
                        .IsRequired();

                    b.Property<decimal?>("Price");

                    b.HasKey("Id");

                    b.HasIndex("CustomerId");

                    b.HasIndex("IsCompleted");

                    b.HasIndex("ManagerId");

                    b.HasIndex("Name");

                    b.ToTable("Projects");
                });
#pragma warning restore 612, 618
        }
    }
}
