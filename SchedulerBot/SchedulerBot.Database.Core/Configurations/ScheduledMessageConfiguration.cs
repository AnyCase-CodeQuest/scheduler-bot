﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchedulerBot.Database.Entities;

namespace SchedulerBot.Database.Core.Configurations
{
	public class ScheduledMessageConfiguration : IEntityTypeConfiguration<ScheduledMessage>
	{
		public void Configure(EntityTypeBuilder<ScheduledMessage> builder)
		{
			builder.HasKey(message => message.Id);
			builder.Property(message => message.Id).ValueGeneratedOnAdd();
			builder.Property(message => message.Schedule).IsRequired();
			builder.Property(message => message.Text).IsRequired();
			builder.Property(message => message.State).IsRequired();
		}
	}
}
