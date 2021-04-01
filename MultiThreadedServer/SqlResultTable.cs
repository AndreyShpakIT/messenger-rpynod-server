using System.Linq;

namespace MultiThreadedServer
{
    class SqlResultTable
    {
        #region Fields

        private Row[] rows;
        private int countColumns;

        #endregion

        #region Properties

        public int CountRows { get => rows?.Length ?? 0; }
        public int CountColumns { get => countColumns; }
        public bool HasRows { get => CountColumns > 0; }

        #endregion

        #region Constructors

        public SqlResultTable(int columns, int rows)
        {
            CreateTable(columns, rows);
        }
        public SqlResultTable(int columns) : this(columns, 0) { }
        public SqlResultTable() : this(0, 0) { }

        #endregion

        #region Methods
        public void CreateTable(int columnsCount, int rowsCount)
        {
            if (columnsCount > -1 && rowsCount > -1)
            {
                countColumns = columnsCount;
                rows = new Row[rowsCount];
                for (int i = 0; i < rowsCount; i++)
                {
                    rows[i] = new Row(columnsCount);
                }
            }
            else
                throw new System.Exception();
        }
        public bool AddRow(Row row)
        {
            if (row.CountColumns == countColumns)
            {
                rows = rows.Append(row).ToArray();
                return true;
            }
            return false;
        }
        public void Clear()
        {
            rows = null;
            countColumns = 0;
        }

        public string[] this[int index]
        {
            get => rows[index].Columns;
            set => rows[index] = new Row(value);
        } 

        #endregion
    }

    struct Row
    {
        public int CountColumns { get; set; }
        public string[] Columns { get; set; }
        public Row(int columnsCount)
        {
            CountColumns = columnsCount;
            Columns = new string[columnsCount];
        }
        public Row(string[] row)
        {
            CountColumns = row.Length;
            Columns = new string[CountColumns];

            for (int i = 0; i < CountColumns; i++)
            {
                Columns[i] = row[i];
            }
        }
        public string this[int index]
        {
            get => Columns[index];
            set => Columns[index] = value;
        }
    }
}
