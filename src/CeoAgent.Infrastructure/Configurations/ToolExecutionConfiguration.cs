using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class ToolExecutionConfiguration : IEntityTypeConfiguration<ToolExecution>
{
    public void Configure(EntityTypeBuilder<ToolExecution> builder)
    {
        builder.ToTable("tool_execution");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ToolKey).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.FailureReason).HasMaxLength(240);
        builder.ComplexProperty(entity => entity.Request, request =>
        {
            request.ToJson("request_json");
            request.Property(entity => entity.ToolKey).HasJsonPropertyName("toolKey");

            var checkAvailabilityRequest = request.ComplexProperty(entity => entity.CheckAvailability);
            checkAvailabilityRequest.HasJsonPropertyName("check_availability");
            checkAvailabilityRequest.Property(entity => entity.Date).HasJsonPropertyName("date");
            checkAvailabilityRequest.Property(entity => entity.PartySize).HasJsonPropertyName("partySize");
            checkAvailabilityRequest.Property(entity => entity.PreferredTime).HasJsonPropertyName("preferredTime");

            var requestHumanHandoffRequest = request.ComplexProperty(entity => entity.RequestHumanHandoff);
            requestHumanHandoffRequest.HasJsonPropertyName("request_human_handoff");
            requestHumanHandoffRequest.Property(entity => entity.Reason).HasJsonPropertyName("reason");
            requestHumanHandoffRequest.Property(entity => entity.Notes).HasJsonPropertyName("notes");

            var createCalendarEventRequest = request.ComplexProperty(entity => entity.CreateCalendarEvent);
            createCalendarEventRequest.HasJsonPropertyName("create_calendar_event");
            createCalendarEventRequest.Property(entity => entity.Start).HasJsonPropertyName("start");
            createCalendarEventRequest.Property(entity => entity.End).HasJsonPropertyName("end");
            createCalendarEventRequest.Property(entity => entity.Summary).HasJsonPropertyName("summary");
        });
        builder.ComplexProperty(entity => entity.Result, result =>
        {
            result.ToJson("result_json");
            result.Property(entity => entity.ToolKey).HasJsonPropertyName("toolKey");

            var checkAvailabilityResult = result.ComplexProperty(entity => entity.CheckAvailability);
            checkAvailabilityResult.HasJsonPropertyName("check_availability");
            checkAvailabilityResult.Property(entity => entity.Available).HasJsonPropertyName("available");
            checkAvailabilityResult.PrimitiveCollection(entity => entity.AlternativeSlots).HasJsonPropertyName("alternativeSlots");
            checkAvailabilityResult.Property(entity => entity.UnavailabilityReason).HasJsonPropertyName("unavailabilityReason");

            var requestHumanHandoffResult = result.ComplexProperty(entity => entity.RequestHumanHandoff);
            requestHumanHandoffResult.HasJsonPropertyName("request_human_handoff");
            requestHumanHandoffResult.Property(entity => entity.HandoffRequested).HasJsonPropertyName("handoffRequested");
            requestHumanHandoffResult.Property(entity => entity.HandoffTicketId).HasJsonPropertyName("handoffTicketId");
            requestHumanHandoffResult.Property(entity => entity.EstimatedPickupAt).HasJsonPropertyName("estimatedPickupAt");

            var createCalendarEventResult = result.ComplexProperty(entity => entity.CreateCalendarEvent);
            createCalendarEventResult.HasJsonPropertyName("create_calendar_event");
            createCalendarEventResult.Property(entity => entity.EventId).HasJsonPropertyName("eventId");
            createCalendarEventResult.Property(entity => entity.EventUrl).HasJsonPropertyName("eventUrl");
        });
        builder.HasIndex(entity => new { entity.CompanyId, entity.IdempotencyKey }).IsUnique();
        builder.HasIndex(entity => new { entity.CompanyId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasOne(entity => entity.Conversation)
            .WithMany()
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.CompanyTool)
            .WithMany(entity => entity.ToolExecutions)
            .HasForeignKey(entity => entity.CompanyToolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.TriggerMessage)
            .WithMany(entity => entity.TriggeredToolExecutions)
            .HasForeignKey(entity => entity.TriggerMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ResultMessage)
            .WithMany(entity => entity.ResultToolExecutions)
            .HasForeignKey(entity => entity.ResultMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
