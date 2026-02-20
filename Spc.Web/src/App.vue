<template>
  <div class="container">
    <header>
      <h1>🧪 SPC Simulator</h1>
    </header>

    <div class="controls">
      <div class="input-group">
        <label>Machine ID:</label>
        <input v-model="machineId" type="text" placeholder="M001" />
      </div>
      <div class="input-group">
        <label>Item:</label>
        <input v-model="itemName" type="text" placeholder="Thickness" />
      </div>
      <button @click="simulate" :disabled="loading">
        {{ loading ? 'Processing...' : '🎲 Simulate' }}
      </button>
    </div>

    <div v-if="lastResult" class="status" :class="lastResult.status.toLowerCase()">
      <span class="status-label">Status:</span>
      <span class="status-value">{{ lastResult.status }}</span>
      <span v-if="lastResult.ruleViolated?.length" class="rules">
        ({{ lastResult.ruleViolated.join(', ') }})
      </span>
    </div>

    <div class="stats" v-if="lastResult">
      <div class="stat">
        <span class="label">Mean:</span>
        <span class="value">{{ lastResult.mean }}</span>
      </div>
      <div class="stat">
        <span class="label">StdDev:</span>
        <span class="value">{{ lastResult.stdDev }}</span>
      </div>
      <div class="stat">
        <span class="label">UCL:</span>
        <span class="value">{{ lastResult.ucl }}</span>
      </div>
      <div class="stat">
        <span class="label">LCL:</span>
        <span class="value">{{ lastResult.lcl }}</span>
      </div>
      <div class="stat">
        <span class="label">CPK:</span>
        <span class="value">{{ lastResult.cpk }}</span>
      </div>
    </div>

    <div class="chart-container">
      <v-chart class="chart" :option="chartOption" autoresize />
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import axios from 'axios'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, MarkLineComponent } from 'echarts/components'

use([CanvasRenderer, LineChart, GridComponent, TooltipComponent, MarkLineComponent])

const machineId = ref('M001')
const itemName = ref('Thickness')
const loading = ref(false)
const lastResult = ref(null)
const chartData = ref([])

const simulate = async () => {
  loading.value = true
  try {
    const res = await axios.get('/api/spc/simulate', {
      params: { machineId: machineId.value, itemName: itemName.value }
    })
    lastResult.value = res.data
    
    // Fetch all data for chart
    const dataRes = await axios.get('/api/spc/data', {
      params: { machineId: machineId.value, itemName: itemName.value, limit: 30 }
    })
    chartData.value = dataRes.data
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

const chartOption = computed(() => {
  const data = chartData.value.map(d => ({
    name: d.timestamp,
    value: [d.timestamp, d.value]
  }))

  const ucl = lastResult.value?.ucl
  const lcl = lastResult.value?.lcl
  const mean = lastResult.value?.mean

  return {
    title: { text: 'Control Chart (X-bar)', left: 'center' },
    tooltip: { trigger: 'axis' },
    xAxis: { 
      type: 'time',
      name: 'Time'
    },
    yAxis: { 
      type: 'value',
      name: 'Value'
    },
    series: [
      {
        type: 'line',
        data: data,
        smooth: true,
        markLine: {
          silent: true,
          symbol: 'none',
          data: [
            { yAxis: ucl, label: { formatter: 'UCL' }, lineStyle: { color: 'red' } },
            { yAxis: mean, label: { formatter: 'CL' }, lineStyle: { color: 'green' } },
            { yAxis: lcl, label: { formatter: 'LCL' }, lineStyle: { color: 'red' } }
          ].filter(d => d.yAxis != null)
        }
      }
    ]
  }
})
</script>

<style>
* { box-sizing: border-box; margin: 0; padding: 0; }

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  background: #f5f7fa;
  min-height: 100vh;
}

.container {
  max-width: 1000px;
  margin: 0 auto;
  padding: 20px;
}

header {
  text-align: center;
  margin-bottom: 20px;
}

h1 { color: #2c3e50; }

.controls {
  display: flex;
  gap: 10px;
  justify-content: center;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.input-group {
  display: flex;
  align-items: center;
  gap: 5px;
}

.input-group label { font-weight: bold; }

input {
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 4px;
}

button {
  padding: 8px 20px;
  background: #3498db;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
}

button:hover { background: #2980b9; }
button:disabled { background: #95a5a6; }

.status {
  text-align: center;
  padding: 15px;
  border-radius: 8px;
  margin-bottom: 20px;
  font-weight: bold;
}

.status.ok { background: #d4edda; color: #155724; }
.status.warning { background: #fff3cd; color: #856404; }
.status.out_of_control { background: #f8d7da; color: #721c24; }

.stats {
  display: flex;
  justify-content: center;
  gap: 20px;
  flex-wrap: wrap;
  margin-bottom: 20px;
}

.stat {
  background: white;
  padding: 10px 20px;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.stat .label { color: #666; margin-right: 5px; }
.stat .value { font-weight: bold; color: #2c3e50; }

.chart-container {
  background: white;
  border-radius: 8px;
  padding: 20px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.chart { height: 400px; width: 100%; }
</style>
