using System.Collections;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);

[MemoryDiagnoser]
[ShortRunJob]
public class Benchmarks
{
    [Benchmark]
    public int WhereForeach()
    {
        MyObject[] objects = Enumerable.Range(1, 10_000).Select(i => new MyObject
        {
            Number = i
        }).ToArray();
    
        return MyWhereForeach(objects, obj => obj.Number % 2 == 0).Count();
    }
    
    [Benchmark]
    public int WhereManual()
    {
        MyObject[] objects = Enumerable.Range(1, 10_000).Select(i => new MyObject
        {
            Number = i
        }).ToArray();
    
        return MyCountManual(MyWhereManual(objects, obj => obj.Number % 2 == 0));
    }
    
    [Benchmark]
    public int WhereLinq()
    {
        MyObject[] objects = Enumerable.Range(1, 10_000).Select(i => new MyObject
        {
            Number = i
        }).ToArray();
    
        return Enumerable.Where(objects, obj => obj.Number % 2 == 0).Count();
    }

    static IEnumerable<T> MyWhereForeach<T>(IEnumerable<T> source, Func<T, bool> filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filter);

        return MyWhereForeachImpl(source, filter);

        IEnumerable<T> MyWhereForeachImpl(IEnumerable<T> source, Func<T, bool> filter)
        {
            foreach (var item in source)
            {
                if (filter(item))
                {
                    yield return item;
                }
            }
        }
    }

    static IEnumerable<T> MyWhereManual<T>(IEnumerable<T> source, Func<T, bool> filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filter);

        if (source is T[] array)
        {
            return new MyWhereEnumerableArray<T>(array, filter);
        }
        
        return new MyWhereEnumerable<T>(source, filter);
    }
    
    static int MyCountManual<T>(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is MyWhereEnumerableArray<T> array)
        {
            return array.Count();
        }

        return source.Count();
    }
}

sealed class MyWhereEnumerable<T>(IEnumerable<T> source, Func<T, bool> filter) : IEnumerable<T>, IEnumerator<T>
{
    private IEnumerator<T>? _enumerator;

    private readonly int _threadId = Environment.CurrentManagedThreadId;

    private int _state;

    public IEnumerator<T> GetEnumerator()
    {
        if (_threadId == Environment.CurrentManagedThreadId && _state == 0)
        {
            _state = 1;
            return this;
        }

        return new MyWhereEnumerable<T>(source, filter)
        {
            _state = 1
        };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool MoveNext()
    {
        if (_state == 1)
        {
            _enumerator = source.GetEnumerator();
            _state = 2;
        }

        try
        {
            while (_enumerator!.MoveNext())
            {
                if (filter(_enumerator.Current))
                {
                    Current = _enumerator.Current;
                    return true;
                }
            }
        }
        catch
        {
            Dispose();
        }

        return false;
    }

    public void Reset() => throw new NotImplementedException();

    public T Current { get; private set; }

    object? IEnumerator.Current => Current;

    public void Dispose()
    {
        _enumerator?.Dispose();
    }
}

sealed class MyWhereEnumerableArray<T>(T[] source, Func<T, bool> filter) : IEnumerable<T>, IEnumerator<T>
{
    private int _state = -1;
    
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    
    private readonly int _length = source.Length;

    public IEnumerator<T> GetEnumerator()
    {
        if (_threadId == Environment.CurrentManagedThreadId && _state == -1)
        {
            return this;
        }
        
        return new MyWhereEnumerableArray<T>(source, filter);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool MoveNext()
    {
        try
        {
            do
            {
                _state++;
                var item = source[_state];
                
                if (filter(item))
                {
                    Current = item;
                    return true;
                }
            } while (_state < _length);
        }
        catch
        {
            Dispose();
        }

        return false;
    }

    public void Reset() => throw new NotImplementedException();

    public T Current { get; private set; }

    object? IEnumerator.Current => Current;

    public void Dispose()
    {
    }

    public int Count()
    {
        var total = 0;

        for (var i = 0; i < _length; i++)
        {
            if (filter(source[i]))
            {
                total++;
            }
        }

        return total;
    }
}

public sealed class MyObject
{
    public int Number { get; set; }
}