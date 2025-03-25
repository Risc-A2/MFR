using System.Collections;

namespace MFR
{
    public class FastList<T> : IEnumerable<T>, IDisposable
    {
        public bool Disposed = false;
        public class ListItem
        {
            public ListItem Next;
            public T item;

            public void Delink()
            {
                item = default;
                Next = null;
            }
        }
 
        private ListItem root = new ListItem();
        private ListItem last = null;
        public int Length = 0;

        public T First
        {
            get
            {
                if (root.Next != null) return root.Next.item;
                else return default(T);
            }
        }

        public class FastIterator : IEnumerator<T>
        {
            private ListItem root;
            public ListItem curr;

            internal FastIterator(FastList<T> ll)
            {
                if (ll.Disposed)
                    throw new ObjectDisposedException("FastList<T>");
                root = ll.root;
                Reset();
            }

            public object Current => curr.item;

            T IEnumerator<T>.Current => curr.item;

            public void Dispose()
            {

            }

            public bool MoveNext()
            {
                try
                {
                    curr = curr.Next;

                    return curr != null;
                }
                catch { return false; }
            }

            public void Reset()
            {
                this.curr = root;
            }
        }
        public void AddRange(T[] collection)
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }
        public T[] ToArray()
        {
            if (Length == 0)
            {
                return []; // Devuelve un array vacío si la lista está vacía
            }
            // Crear un array del tamaño adecuado
            T[] array = new T[Length];

            // Recorrer la lista y copiar los elementos al array
            int index = 0;
            foreach (var current in this)
            {
                if (current == null)
                {
                    throw new InvalidOperationException("La lista contiene elementos nulos.");
                }
                array[index] = current;
                index++;
            }

            return array;
        }
        public bool Remove(T item)
        {
            ListItem current = root.Next; // Comenzamos desde el primer elemento
            ListItem previous = root; // Mantener referencia al nodo anterior

            // Recorremos la lista
            while (current != null)
            {
                // Comparar el elemento actual con el que queremos eliminar
                if (EqualityComparer<T>.Default.Equals(current.item, item))
                {
                    // Si encontramos el elemento, lo removemos
                    previous.Next = current.Next; // Salta el nodo actual

                    // Si el elemento eliminado es el último, actualizamos 'last'
                    if (current == last)
                    {
                        last = previous;
                    }

                    Length--;
                    // Opcional: Establecer current a null para liberar su referencia
                    current = null;

                    return true; // Devolvemos true indicando que se eliminó el elemento
                }

                previous = current; // Avanzamos al siguiente nodo
                current = current.Next; // Avanzamos al siguiente nodo
            }

            return false; // Devolvemos false si no se encontró el elemento
        }


        public void Add(T item)
        {
            ListItem li = new ListItem { item = item };
            if (last != null)
            {
                last.Next = li;
            }
            else
            {
                root.Next = li;
            }
            last = li;
            Length++;
        }

        public T Pop()
        {
            ListItem el = root.Next;
            root.Next = el.Next;
            Length--;
            return el.item;
        }

        public FastIterator Iterate()
        {
            return new FastIterator(this);
        }

        public bool ZeroLen => root.Next == null;

        public IEnumerator<T> FastIterate()
        {
            return new FastIterator(this);
        }

        public void Unlink()
        {
            root.Next = null;
            last = null;
            Length = 0;
        }

        public int Count()
        {
            int cnt = 0;

            ListItem li = root.Next;
            while (li != null)
            {
                cnt++;
                li = li.Next;
            }

            return cnt;
        }

        public bool Any()
        {
            return root.Next != null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return FastIterate();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return FastIterate();
        }

        public void Dispose()
        {
            if (Disposed)
                return;
            // TODO release managed resources here
            root.Next = null;
            last = null;
            Length = 0;
            Disposed = true;
        }
    }
}