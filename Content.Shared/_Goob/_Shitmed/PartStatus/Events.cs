// Goob import.
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Goob._Shitmed.PartStatus.Events;

[Serializable, NetSerializable]
public sealed class GetPartStatusEvent : EntityEventArgs
{
    public NetEntity Uid { get; }
    public GetPartStatusEvent(NetEntity uid)
    {
        Uid = uid;
    }
}

/// <summary>
/// Raised when an entity with woundables is examined. Copy of ExaminedEvent but not inheriting.
/// </summary>
public sealed class PartStatusExaminedEvent : EntityEventArgs
{
    private FormattedMessage Message { get; }
    private List<ExamineMessagePart> Parts { get; } = new();

    public EntityUid Examiner { get; }
    public EntityUid Examined { get; }

    private ExamineMessagePart? _currentGroupPart;

    public PartStatusExaminedEvent(FormattedMessage message, EntityUid examined, EntityUid examiner)
    {
        Message = message;
        Examined = examined;
        Examiner = examiner;
    }

    public FormattedMessage GetTotalMessage()
    {
        int Comparison(ExamineMessagePart a, ExamineMessagePart b)
        {
            if (a.Priority != b.Priority)
                return -a.Priority.CompareTo(b.Priority);

            if (a.Group != b.Group)
                return string.Compare(a.Group, b.Group, StringComparison.Ordinal);

            return string.Compare(a.Message.ToString(), b.Message.ToString(), StringComparison.Ordinal);
        }

        var parts = Parts.ToList();
        var totalMessage = new FormattedMessage(Message);
        parts.Sort(Comparison);

        foreach (var part in parts)
        {
            totalMessage.AddMessage(part.Message);
            if (part.DoNewLine && parts.Last() != part)
                totalMessage.PushNewline();
        }

        totalMessage.TrimEnd();

        return totalMessage;
    }

    public ExamineGroupDisposable PushGroup(string groupName, int priority = 0)
    {
        DebugTools.Assert(_currentGroupPart == null);
        _currentGroupPart = new ExamineMessagePart(new FormattedMessage(), priority, false, groupName);
        return new ExamineGroupDisposable(this);
    }

    private void PopGroup()
    {
        DebugTools.Assert(_currentGroupPart != null);
        if (_currentGroupPart is { } current && !current.Message.IsEmpty)
        {
            Parts.Add(current);
        }

        _currentGroupPart = null;
    }

    public void PushMessage(FormattedMessage message, int priority = 0)
    {
        if (message.Nodes.Count == 0)
            return;

        if (_currentGroupPart is { } current)
        {
            message.PushNewline();
            current.Message.AddMessage(message);
        }
        else
        {
            Parts.Add(new ExamineMessagePart(message, priority, true, null));
        }
    }

    public void PushMarkup(string markup, int priority = 0)
    {
        PushMessage(FormattedMessage.FromMarkupOrThrow(markup), priority);
    }

    public void PushText(string text, int priority = 0)
    {
        var msg = new FormattedMessage();
        msg.AddText(text);
        PushMessage(msg, priority);
    }

    public void AddMessage(FormattedMessage message, int priority = 0)
    {
        if (message.Nodes.Count == 0)
            return;

        if (_currentGroupPart is { } current)
        {
            current.Message.AddMessage(message);
        }
        else
        {
            Parts.Add(new ExamineMessagePart(message, priority, false, null));
        }
    }

    public void AddMarkup(string markup, int priority = 0)
    {
        AddMessage(FormattedMessage.FromMarkupOrThrow(markup), priority);
    }

    public void AddText(string text, int priority = 0)
    {
        var msg = new FormattedMessage();
        msg.AddText(text);
        AddMessage(msg, priority);
    }

    public void AddGroupMessage(FormattedMessage message, int priority = 0)
    {
        if (_currentGroupPart == null)
        {
            _currentGroupPart = new ExamineMessagePart(new FormattedMessage(), priority, false, null);
        }

        var current = _currentGroupPart.Value;
        current.Message.AddMessage(message);
        _currentGroupPart = current;
    }

    public void AddGroupMarkup(string markup, int priority = 0)
    {
        AddGroupMessage(FormattedMessage.FromMarkupOrThrow(markup), priority);
    }

    public void AddGroupText(string text, int priority = 0)
    {
        var msg = new FormattedMessage();
        msg.AddText(text);
        AddGroupMessage(msg, priority);
    }

    public readonly record struct ExamineMessagePart(FormattedMessage Message, int Priority, bool DoNewLine, string? Group);

    public readonly struct ExamineGroupDisposable : IDisposable
    {
        private readonly PartStatusExaminedEvent _parent;

        public ExamineGroupDisposable(PartStatusExaminedEvent parent)
        {
            _parent = parent;
        }

        public void Dispose()
        {
            _parent.PopGroup();
        }
    }
}
