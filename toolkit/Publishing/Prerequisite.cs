namespace CoApp.Developer.Toolkit.Publishing {
    using System;
    using System.Threading.Tasks;


    /*
    public class PrerequisiteValue<T> where T:struct  {
        private readonly Func<Task> _prerequisiteFunction;
        private readonly Func<T> _initializer;
        public T? Value { get; set; }
        public PrerequisiteValue(Func<Task> prerequisiteFunction ) {
            _prerequisiteFunction = prerequisiteFunction;
        }
        public PrerequisiteValue(Func<Task> prerequisiteFunction, Func<T> initializer ) {
            _prerequisiteFunction = prerequisiteFunction;
            _initializer = initializer;
        }

        public static implicit operator T(PrerequisiteValue<T> instance ) {
            if (instance.Value == null) {
                instance._prerequisiteFunction().Wait();
                if (instance._initializer != null) {
                    instance.Value = instance._initializer();
                }
            }
            
            if (instance.Value == null) {
                throw new Exception("Expected PrerequisiteValue Value not to be null.");
            }
            return (T)instance.Value;
        }
    }
    */

    public class Prerequisite<T> {
        private bool _initialized;
        private readonly Func<Task> _prerequisiteFunction;
        private readonly Func<T> _initializer;
        private T _value;
        public T Value {
            get {
                if (!_initialized) {
                    _prerequisiteFunction().Wait();
                    if (_initializer != null) {
                        Value = _initializer();
                    }
                }
                return _value;
            }
            set {
                _value = value;
                _initialized = true;
            }
        }
      
        public Prerequisite(Func<Task> prerequisiteFunction) {
            _prerequisiteFunction = prerequisiteFunction;
        }
        public Prerequisite(Func<Task> prerequisiteFunction, Func<T> initializer) {
            _prerequisiteFunction = prerequisiteFunction;
            _initializer = initializer;
        }
        public static implicit operator T(Prerequisite<T> instance) {
            return instance.Value;
        }
        public override string ToString() {
            return _value.ToString();
        }
    }
}