using System;
using System.Collections.Generic;
using SpaceTradeEngine.ECS;

namespace SpaceTradeEngine.AI
{
    /// <summary>
    /// Result of a behavior node execution
    /// </summary>
    public enum NodeStatus
    {
        Success,    // Node completed successfully
        Failure,    // Node failed
        Running     // Node is still executing (for async operations)
    }

    /// <summary>
    /// Context passed to behavior nodes during execution
    /// </summary>
    public class BehaviorContext
    {
        public Entity Entity { get; set; }
        public float DeltaTime { get; set; }
        public Dictionary<string, object> Blackboard { get; private set; }

        public BehaviorContext(Entity entity, float deltaTime)
        {
            Entity = entity;
            DeltaTime = deltaTime;
            Blackboard = new Dictionary<string, object>();
        }

        // Helper methods for blackboard
        public void SetValue<T>(string key, T value) => Blackboard[key] = value;
        
        public T GetValue<T>(string key)
        {
            if (Blackboard.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return default(T);
        }

        public bool HasValue(string key) => Blackboard.ContainsKey(key);
        
        public void ClearValue(string key) => Blackboard.Remove(key);
    }

    /// <summary>
    /// Base class for all behavior tree nodes
    /// </summary>
    public abstract class BehaviorNode
    {
        public string Name { get; set; }

        protected BehaviorNode(string name = null)
        {
            Name = name ?? GetType().Name;
        }

        public abstract NodeStatus Execute(BehaviorContext context);

        // Optional callbacks
        public virtual void OnEnter(BehaviorContext context) { }
        public virtual void OnExit(BehaviorContext context) { }
    }

    #region Composite Nodes

    /// <summary>
    /// Base class for nodes with children
    /// </summary>
    public abstract class CompositeNode : BehaviorNode
    {
        protected List<BehaviorNode> Children { get; } = new List<BehaviorNode>();

        protected CompositeNode(string name = null) : base(name) { }

        public CompositeNode AddChild(BehaviorNode child)
        {
            Children.Add(child);
            return this;
        }
    }

    /// <summary>
    /// Executes children in sequence until one fails
    /// Returns Success if all children succeed
    /// </summary>
    public class SequenceNode : CompositeNode
    {
        public SequenceNode(string name = null) : base(name) { }

        public override NodeStatus Execute(BehaviorContext context)
        {
            foreach (var child in Children)
            {
                var status = child.Execute(context);
                
                if (status == NodeStatus.Failure)
                    return NodeStatus.Failure;
                
                if (status == NodeStatus.Running)
                    return NodeStatus.Running;
            }
            
            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// Executes children in sequence until one succeeds
    /// Returns Failure if all children fail
    /// </summary>
    public class SelectorNode : CompositeNode
    {
        public SelectorNode(string name = null) : base(name) { }

        public override NodeStatus Execute(BehaviorContext context)
        {
            foreach (var child in Children)
            {
                var status = child.Execute(context);
                
                if (status == NodeStatus.Success)
                    return NodeStatus.Success;
                
                if (status == NodeStatus.Running)
                    return NodeStatus.Running;
            }
            
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Executes all children in parallel
    /// </summary>
    public class ParallelNode : CompositeNode
    {
        public enum ParallelPolicy
        {
            RequireAll,     // All must succeed
            RequireOne      // At least one must succeed
        }

        public ParallelPolicy Policy { get; set; }

        public ParallelNode(ParallelPolicy policy = ParallelPolicy.RequireAll, string name = null) : base(name)
        {
            Policy = policy;
        }

        public override NodeStatus Execute(BehaviorContext context)
        {
            int successCount = 0;
            int failureCount = 0;

            foreach (var child in Children)
            {
                var status = child.Execute(context);
                
                if (status == NodeStatus.Success)
                    successCount++;
                else if (status == NodeStatus.Failure)
                    failureCount++;
                else if (status == NodeStatus.Running)
                    {
                        // keep running until children settle
                    }
            }

            if (Policy == ParallelPolicy.RequireAll)
            {
                if (failureCount > 0)
                    return NodeStatus.Failure;
                if (successCount == Children.Count)
                    return NodeStatus.Success;
                return NodeStatus.Running;
            }
            else // RequireOne
            {
                if (successCount > 0)
                    return NodeStatus.Success;
                if (failureCount == Children.Count)
                    return NodeStatus.Failure;
                return NodeStatus.Running;
            }
        }
    }

    #endregion

    #region Decorator Nodes

    /// <summary>
    /// Base class for nodes that modify a single child's behavior
    /// </summary>
    public abstract class DecoratorNode : BehaviorNode
    {
        protected BehaviorNode Child { get; set; }

        protected DecoratorNode(BehaviorNode child, string name = null) : base(name)
        {
            Child = child;
        }
    }

    /// <summary>
    /// Inverts the result of the child node
    /// </summary>
    public class InverterNode : DecoratorNode
    {
        public InverterNode(BehaviorNode child, string name = null) : base(child, name) { }

        public override NodeStatus Execute(BehaviorContext context)
        {
            var status = Child.Execute(context);
            
            if (status == NodeStatus.Success)
                return NodeStatus.Failure;
            if (status == NodeStatus.Failure)
                return NodeStatus.Success;
            
            return NodeStatus.Running;
        }
    }

    /// <summary>
    /// Always returns Success regardless of child result
    /// </summary>
    public class SucceederNode : DecoratorNode
    {
        public SucceederNode(BehaviorNode child, string name = null) : base(child, name) { }

        public override NodeStatus Execute(BehaviorContext context)
        {
            Child.Execute(context);
            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// Repeats child node N times or until it fails
    /// </summary>
    public class RepeaterNode : DecoratorNode
    {
        public int RepeatCount { get; set; }
        private int _currentCount = 0;

        public RepeaterNode(BehaviorNode child, int repeatCount = -1, string name = null) : base(child, name)
        {
            RepeatCount = repeatCount; // -1 = infinite
        }

        public override NodeStatus Execute(BehaviorContext context)
        {
            if (RepeatCount == -1) // Infinite
            {
                var status = Child.Execute(context);
                return status == NodeStatus.Failure ? NodeStatus.Failure : NodeStatus.Running;
            }

            if (_currentCount >= RepeatCount)
            {
                _currentCount = 0;
                return NodeStatus.Success;
            }

            var result = Child.Execute(context);
            
            if (result == NodeStatus.Success)
            {
                _currentCount++;
                return _currentCount >= RepeatCount ? NodeStatus.Success : NodeStatus.Running;
            }
            
            if (result == NodeStatus.Failure)
            {
                _currentCount = 0;
                return NodeStatus.Failure;
            }

            return NodeStatus.Running;
        }
    }

    /// <summary>
    /// Runs child until it succeeds
    /// </summary>
    public class RetryNode : DecoratorNode
    {
        public int MaxAttempts { get; set; }
        private int _attempts = 0;

        public RetryNode(BehaviorNode child, int maxAttempts = -1, string name = null) : base(child, name)
        {
            MaxAttempts = maxAttempts; // -1 = infinite
        }

        public override NodeStatus Execute(BehaviorContext context)
        {
            var status = Child.Execute(context);

            if (status == NodeStatus.Success)
            {
                _attempts = 0;
                return NodeStatus.Success;
            }

            if (status == NodeStatus.Failure)
            {
                _attempts++;
                
                if (MaxAttempts != -1 && _attempts >= MaxAttempts)
                {
                    _attempts = 0;
                    return NodeStatus.Failure;
                }
            }

            return NodeStatus.Running;
        }
    }

    /// <summary>
    /// Limits execution time of child node
    /// </summary>
    public class TimeoutNode : DecoratorNode
    {
        public float TimeoutSeconds { get; set; }
        private float _elapsedTime = 0f;

        public TimeoutNode(BehaviorNode child, float timeoutSeconds, string name = null) : base(child, name)
        {
            TimeoutSeconds = timeoutSeconds;
        }

        public override NodeStatus Execute(BehaviorContext context)
        {
            _elapsedTime += context.DeltaTime;

            if (_elapsedTime >= TimeoutSeconds)
            {
                _elapsedTime = 0f;
                return NodeStatus.Failure;
            }

            var status = Child.Execute(context);
            
            if (status != NodeStatus.Running)
                _elapsedTime = 0f;

            return status;
        }
    }

    #endregion

    #region Leaf Nodes (Action & Condition)

    /// <summary>
    /// Leaf node that performs an action
    /// </summary>
    public class ActionNode : BehaviorNode
    {
        private Func<BehaviorContext, NodeStatus> _action;

        public ActionNode(Func<BehaviorContext, NodeStatus> action, string name = null) : base(name)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override NodeStatus Execute(BehaviorContext context)
        {
            return _action(context);
        }
    }

    /// <summary>
    /// Leaf node that checks a condition
    /// </summary>
    public class ConditionNode : BehaviorNode
    {
        private Func<BehaviorContext, bool> _condition;

        public ConditionNode(Func<BehaviorContext, bool> condition, string name = null) : base(name)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public override NodeStatus Execute(BehaviorContext context)
        {
            return _condition(context) ? NodeStatus.Success : NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Always returns Success (useful for debugging/testing)
    /// </summary>
    public class SuccessNode : BehaviorNode
    {
        public SuccessNode(string name = null) : base(name) { }

        public override NodeStatus Execute(BehaviorContext context)
        {
            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// Always returns Failure (useful for debugging/testing)
    /// </summary>
    public class FailureNode : BehaviorNode
    {
        public FailureNode(string name = null) : base(name) { }

        public override NodeStatus Execute(BehaviorContext context)
        {
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Waits for a specified duration
    /// </summary>
    public class WaitNode : BehaviorNode
    {
        public float WaitTime { get; set; }
        private float _elapsedTime = 0f;

        public WaitNode(float waitTime, string name = null) : base(name)
        {
            WaitTime = waitTime;
        }

        public override NodeStatus Execute(BehaviorContext context)
        {
            _elapsedTime += context.DeltaTime;

            if (_elapsedTime >= WaitTime)
            {
                _elapsedTime = 0f;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }
    }

    #endregion

    /// <summary>
    /// Main behavior tree class
    /// </summary>
    public class BehaviorTree
    {
        public BehaviorNode Root { get; set; }
        public Entity Owner { get; set; }
        public bool IsEnabled { get; set; } = true;

        private BehaviorContext _context;

        public BehaviorTree(Entity owner, BehaviorNode root)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public NodeStatus Tick(float deltaTime)
        {
            if (!IsEnabled)
                return NodeStatus.Failure;

            _context = new BehaviorContext(Owner, deltaTime);
            return Root.Execute(_context);
        }

        public void Reset()
        {
            _context?.Blackboard.Clear();
        }

        public T GetBlackboardValue<T>(string key)
        {
            if (_context == null)
                return default;
            return _context.GetValue<T>(key);
        }

        public void SetBlackboardValue<T>(string key, T value)
        {
            _context?.SetValue(key, value);
        }
    }
}
