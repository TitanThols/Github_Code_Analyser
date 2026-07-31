import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';

interface SeverityDatum {
  name: string;
  value: number;
  color: string;
}

interface SeverityDonutChartProps {
  data: SeverityDatum[];
}

const SeverityDonutChart = ({ data }: SeverityDonutChartProps) => {
  return (
    <div className="chart-shell">
      <ResponsiveContainer width="100%" height={260}>
        <PieChart>
          <Pie
            data={data}
            dataKey="value"
            nameKey="name"
            innerRadius={60}
            outerRadius={100}
            paddingAngle={2}
          >
            {data.map((entry) => (
              <Cell key={entry.name} fill={entry.color} />
            ))}
          </Pie>
          <Tooltip />
          <Legend verticalAlign="bottom" height={24} />
        </PieChart>
      </ResponsiveContainer>
    </div>
  );
};

export default SeverityDonutChart;
