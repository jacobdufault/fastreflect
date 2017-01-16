using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace FastReflect {
    public interface IBaseInterface { }
    public interface IDerivedInterface { }
    public interface IExplicitInterface {
        void MyExplicitMethod();
    }

    public static class CallCounts {
        public static int MyBaseType_MyExplicitMethodCallCount = 0;
        public static int MyDerivedType_MyExplicitMethodCallCount = 0;
        public static int Accelerated = 0;
    }

    public struct AccelerationType {
        public float Field;
        public float Method() { return Field; }
    }

    public class MyBaseType : IBaseInterface, IExplicitInterface {
        public float BaseField;
        public void BaseMethod() { }
        public virtual void BaseVirtualMethod() { }
        void IExplicitInterface.MyExplicitMethod() { ++CallCounts.MyBaseType_MyExplicitMethodCallCount; }
    }

    public class MyDerivedType : MyBaseType, IDerivedInterface, IExplicitInterface {
        public new float BaseField;
        public float ChildField;
        public void ChildMethod() { }
        public void ChildMethod(string param) { }
        public virtual void BaseChildMethod() { }
        void IExplicitInterface.MyExplicitMethod() { ++CallCounts.MyDerivedType_MyExplicitMethodCallCount; }
    }

    public class AccelerationProvider {
        public static frTypeAotData Provider_MyBaseType = new frTypeAotData {
            ProviderFor = typeof(AccelerationType),
            FieldAotData = new frField.AotData[] {
                new frField.AotData {
                    FieldName = "Field",
                    Read = (o) => { CallCounts.Accelerated++; return ((AccelerationType)o).Field; },
                    Write = (ref object o, object v) => { CallCounts.Accelerated++; var u = (AccelerationType)o; u.Field = (float)v; o = u; }
                }
            },
            MethodAotData = new frMethod.AotData[] {
                new frMethod.AotData {
                    MethodName = "Method",
                    Parameters = new Type[] {},
                    Invoke = (o, args) => { CallCounts.Accelerated++; return ((AccelerationType)o).Method(); }
                },
            }
        };
    }

    public class frTests {
        private static IEnumerable<string> DeclaredFieldNames(frType type) {
            return type.Fields.Select(f => f.Name);
        }
        private static IEnumerable<string> DeclaredMethodNames(frType type) {
            return type.Methods.Select(m => m.RawMethod.Name);
        }
        private static IEnumerable<Type> Interfaces(frType type) {
            return type.Interfaces.Select(t => t.RawType);
        }

        [Test]
        public void Generation() {
            var generator = new frGenerator();
            // TODO: Find a good way to verify the result. Right now it's just
            //       manual inspection and making sure the function doesn't crash.
            string result = generator.GenerateForType("foo", frManager.Instance.Get(typeof(MyDerivedType)));
            Console.WriteLine(result);
        }

        [Test]
        public void Sanity() {
            var manager = new frManager();

            // Verify built in types are supported.
            Assert.IsNotNull(manager.Get(typeof(object)));
            Assert.IsNotNull(manager.Get(typeof(int)));

            // Verify custom types are supported.
            frType myBaseType = manager.Get(typeof(MyBaseType));
            Assert.IsNotNull(myBaseType);

            // fields/methods on MyTest
            Assert.AreEqual(typeof(MyBaseType), myBaseType.RawType);
            CollectionAssert.AreEquivalent(new[] { "BaseField" }, DeclaredFieldNames(myBaseType));
            CollectionAssert.AreEquivalent(new[] { "BaseMethod", "BaseVirtualMethod", "FastReflect.IExplicitInterface.MyExplicitMethod" }, DeclaredMethodNames(myBaseType));

            // parent type is Object
            Assert.AreEqual(typeof(object), myBaseType.Parent.RawType);
        }

        [Test]
        public void FrTypesAreCached() {
            var manager = new frManager();
            frType myDerivedType = manager.Get(typeof(MyDerivedType));
            Assert.IsTrue(ReferenceEquals(myDerivedType, manager.Get(typeof(MyDerivedType))));
            Assert.IsTrue(ReferenceEquals(myDerivedType.Parent, manager.Get(typeof(MyBaseType))));
        }

        [Test]
        public void TestLocalDeclaration() {
            var manager = new frManager();
            frType myDerivedType = manager.Get(typeof(MyDerivedType));

            // Verify parent/rawtype
            Assert.AreEqual(typeof(MyDerivedType), myDerivedType.RawType);
            Assert.AreEqual(typeof(MyBaseType), myDerivedType.Parent.RawType);

            // Verify fields/methods
            // MyDerivedType
            CollectionAssert.AreEquivalent(new[] { "BaseField", "ChildField" }, DeclaredFieldNames(myDerivedType));
            CollectionAssert.AreEquivalent(new[] { "BaseChildMethod", "ChildMethod", "ChildMethod", "FastReflect.IExplicitInterface.MyExplicitMethod" }, DeclaredMethodNames(myDerivedType));
            // MyBaseType
            CollectionAssert.AreEquivalent(new[] { "BaseField" }, DeclaredFieldNames(myDerivedType.Parent));
            CollectionAssert.AreEquivalent(new[] { "BaseMethod", "BaseVirtualMethod", "FastReflect.IExplicitInterface.MyExplicitMethod" }, DeclaredMethodNames(myDerivedType.Parent));
        }

        [Test]
        public void TestLookupWithNewFields() {
            var manager = new frManager();
            frType myDerivedType = manager.Get(typeof(MyDerivedType));
            frType myBaseType = manager.Get(typeof(MyBaseType));

            // new fields
            Assert.AreEqual(typeof(MyDerivedType), myDerivedType.GetDeclaredFieldByName("BaseField").RawMember.DeclaringType);
            Assert.AreEqual(typeof(MyBaseType), myBaseType.GetDeclaredFieldByName("BaseField").RawMember.DeclaringType);
            CollectionAssert.AreEquivalent(new[] {
                myDerivedType.GetDeclaredFieldByName("BaseField"),
                myBaseType.GetDeclaredFieldByName("BaseField")
            }, myDerivedType.GetFlattenedFieldsByName("BaseField"));
        }

        [Test]
        public void TestLookupWithOverloadedMethod() {
            var manager = new frManager();
            frType myDerivedType = manager.Get(typeof(MyDerivedType));

            frMethod[] methods = myDerivedType.GetDeclaredMethodsByName("ChildMethod");
            Assert.IsTrue(methods.Length == 2);
            Assert.AreEqual(methods[0].RawMethod.Name, methods[1].RawMethod.Name);
            Assert.AreNotEqual(methods[0], methods[1]);
            Assert.AreNotEqual(methods[0].RawMethod.GetParameters().Length, methods[1].RawMethod.GetParameters().Length);
        }

        [Test]
        public void TestInterfaces() {
            var manager = new frManager();
            frType myBaseType = manager.Get(typeof(MyBaseType));
            frType myDerivedType = manager.Get(typeof(MyDerivedType));

            CollectionAssert.AreEquivalent(new[] { typeof(IBaseInterface), typeof(IExplicitInterface) }, Interfaces(myBaseType));
            CollectionAssert.AreEquivalent(new[] { typeof(IDerivedInterface), typeof(IBaseInterface), typeof(IExplicitInterface) }, Interfaces(myDerivedType));

            CallCounts.MyBaseType_MyExplicitMethodCallCount = 0;
            myBaseType.GetInterface<IExplicitInterface>().GetDeclaredMethodsByName("MyExplicitMethod")[0].Invoke(new MyBaseType(), null);
            Assert.AreEqual(1, CallCounts.MyBaseType_MyExplicitMethodCallCount);

            CallCounts.MyDerivedType_MyExplicitMethodCallCount = 0;
            myDerivedType.GetInterface<IExplicitInterface>().GetDeclaredMethodsByName("MyExplicitMethod")[0].Invoke(new MyDerivedType(), null);
            Assert.AreEqual(1, CallCounts.MyDerivedType_MyExplicitMethodCallCount);
        }

        [Test]
        public void TestAcceleration() {
            var manager = new frManager(typeof(AccelerationProvider));
            frType accelerationType = manager.Get(typeof(AccelerationType));

            var instance = new AccelerationType();
            instance.Field = 10;

            CallCounts.Accelerated = 0;
            accelerationType.GetDeclaredMethodsByName("Method")[0].Invoke(instance, null);
            Assert.AreEqual(1, CallCounts.Accelerated);

            CallCounts.Accelerated = 0;
            Assert.AreEqual(10, accelerationType.GetDeclaredFieldByName("Field").Read<object>(instance));
            Assert.AreEqual(1, CallCounts.Accelerated);

            CallCounts.Accelerated = 0;
            accelerationType.GetDeclaredFieldByName("Field").Write(ref instance, 20f);
            Assert.AreEqual(20f, instance.Field);
            Assert.AreEqual(1, CallCounts.Accelerated);
        }

        [Test]
        public void TestAccelerationPerformanceDifference() {
            frType reflectedMyBaseType = (new frManager()).Get(typeof(AccelerationType));
            frType acceleratedMyBaseType = (new frManager(typeof(AccelerationProvider))).Get(typeof(AccelerationType));

            frMethod reflectedMethod = reflectedMyBaseType.GetDeclaredMethodsByName("Method")[0];
            frMethod acceleratedMethod = acceleratedMyBaseType.GetDeclaredMethodsByName("Method")[0];

            const int ITERATION_COUNT = 50000;
            var instance = new AccelerationType();

            var reflectedTime = Stopwatch.StartNew();
            for (int i = 0; i < ITERATION_COUNT; ++i)
                reflectedMethod.Invoke(instance, null);
            reflectedTime.Stop();

            var acceleratedTime = Stopwatch.StartNew();
            for (int i = 0; i < ITERATION_COUNT; ++i)
                acceleratedMethod.Invoke(instance, null);
            acceleratedTime.Stop();

            Console.WriteLine(string.Format("Reflected ticks: {0}, Accelerated ticks: {1}", reflectedTime.ElapsedTicks, acceleratedTime.ElapsedTicks));
            Assert.IsTrue(acceleratedTime.ElapsedTicks * 2 < reflectedTime.ElapsedTicks);
        }
    }
}