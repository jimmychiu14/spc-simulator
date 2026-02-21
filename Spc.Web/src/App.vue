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
      <div class="input-group">
        <label>Subgroup Size (n):</label>
        <select v-model.number="subgroupSize">
          <option :value="3">3</option>
          <option :value="4">4</option>
          <option :value="5">5</option>
          <option :value="6">6</option>
          <option :value="7">7</option>
          <option :value="8">8</option>
        </select>
      </div>
      <button @click="simulateXBarR" :disabled="loading">
        {{ loading ? 'Processing...' : '🎲 Simulate X-bar + R' }}
      </button>
    </div>

    <!-- X-bar Chart Status -->
    <div v-if="xbarRData" class="status" :class="xbarRData.xBarStatus.toLowerCase()">
      <span class="status-label">X-bar Status:</span>
      <span class="status-value">{{ xbarRData.xBarStatus }}</span>
      <span v-if="xbarRData.xBarRules?.length" class="rules">
        ({{ xbarRData.xBarRules.join(', ') }})
      </span>
    </div>

    <!-- X-bar Stats -->
    <div class="stats" v-if="xbarRData">
      <div class="stat">
        <span class="label">X̿ (Mean):</span>
        <span class="value">{{ xbarRData.overallMean }}</span>
      </div>
      <div class="stat">
        <span class="label">σ (Est.):</span>
        <span class="value">{{ xbarRData.overallSigma }}</span>
      </div>
      <div class="stat">
        <span class="label">UCL:</span>
        <span class="value">{{ xbarRData.xBarUcl }}</span>
      </div>
      <div class="stat">
        <span class="label">LCL:</span>
        <span class="value">{{ xbarRData.xBarLcl }}</span>
      </div>
      <div class="stat">
        <span class="label">CPK:</span>
        <span class="value">{{ xbarRData.xBarCpk }}</span>
      </div>
    </div>

    <!-- X-bar Chart -->
    <div class="chart-container">
      <v-chart class="chart" :option="xBarChartOption" autoresize />
    </div>

    <!-- R Chart Status -->
    <div v-if="xbarRData" class="status" :class="xbarRData.rStatus.toLowerCase()">
      <span class="status-label">R Chart Status:</span>
      <span class="status-value">{{ xbarRData.rStatus }}</span>
      <span v-if="xbarRData.rRules?.length" class="rules">
        ({{ xbarRData.rRules.join(', ') }})
      </span>
    </div>

    <!-- R Chart Stats -->
    <div class="stats" v-if="xbarRData">
      <div class="stat">
        <span class="label">R̅ (Avg Range):</span>
        <span class="value">{{ xbarRData.rMean }}</span>
      </div>
      <div class="stat">
        <span class="label">UCL:</span>
        <span class="value">{{ xbarRData.rUcl }}</span>
      </div>
      <div class="stat">
        <span class="label">LCL:</span>
        <span class="value">{{ xbarRData.rLcl ?? 0 }}</span>
      </div>
    </div>

    <!-- R Chart -->
    <div class="chart-container">
      <v-chart class="chart" :option="rChartOption" autoresize />
    </div>

    <!-- Legacy Single Value Chart -->
    <details class="legacy-section">
      <summary>📊 Legacy X-bar Chart (Single Values)</summary>
      
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
    </details>
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

// API base URL
const API_BASE = import.meta.env.VITE_API_URL || 'https://spc-api-ap7g.onrender.com'

// Legacy single value mode
const machineId = ref('M001')
const itemName = ref('Thickness')
const loading = ref(false)
const lastResult = ref(null)
const chartData = ref([])

// X-bar + R chart mode
const subgroupSize = ref(5)
const xbarRData = ref(null)

// Simulate X-bar + R chart
const simulateXBarR = async () => {
  loading.value = true
  try {
    const res = await axios.get(`${API_BASE}/api/spc/simulate-xbar-r`, {
      params: { 
        machineId: machineId.value, 
        itemName: itemName.value,
        subgroupSize: subgroupSize.value
      }
    })
    xbarRData.value = res.data
    
    // Fetch full chart data
    const dataRes = await axios.get(`${API_BASE}/api/spc/xbar-r-data`, {
      params: { 
        machineId: machineId.value, 
        itemName: itemName.value,
        subgroupSize: subgroupSize.value,
        numSubgroups: 25
      }
    })
    xbarRData.value = dataRes.data
  } catch (e) {
    console.error(e)
    alert('API Error: ' + e.message)
  } finally {
    loading.value = false
  }
}

// Legacy simulate
const simulate = async () => {
  loading.value = true
  try {
    const res = await axios.get(`${API_BASE}/api/spc/simulate`, {
      params: { machineId: machineId.value, itemName: itemName.value }
    })
    lastResult.value = res.data
    
    const dataRes = await axios.get(`${API_BASE}/api/spc/data`, {
      params: { machineId: machineId.value, itemName: itemName.value, limit: 30 }
    })
    chartData.value = dataRes.data
  } catch (e) {
    console.error(e)
    alert('API Error: ' + e.message)
  } finally {
    loading.value = false
  }
}

// Legacy chart option
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
    xAxis: { type: 'time', name: 'Time' },
    yAxis: { type: 'value', name: 'Value' },
    series: [{
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
    }]
  }
})

// X-bar chart option
const xBarChartOption = computed(() => {
  if (!xbarRData.value?.xBarData) return {}
  
  const data = xbarRData.value.xBarData.map(d => ({
    name: d.subgroupIndex,
    value: [d.subgroupIndex, d.value]
  }))

  const ucl = xbarRData.value.xBarUcl
  const lcl = xbarRData.value.xBarLcl
  const mean = xbarRData.value.overallMean

  return {
    title: { text: 'X-bar Chart (Subgroup Means)', left: 'center' },
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', name: 'Subgroup', data: xbarRData.value.xBarData.map(d => d.subgroupIndex) },
    yAxis: { type: 'value', name: 'X̿' },
    series: [{
      type: 'line',
      data: data.map(d => d.value[1]),
      smooth: true,
      markLine: {
        silent: true,
        symbol: 'none',
        data: [
          { yAxis: ucl, label: { formatter: `UCL\n${ucl}` }, lineStyle: { color: 'red', type: 'dashed' } },
          { yAxis: mean, label: { formatter: `CL\n${mean}` }, lineStyle: { color: 'green' } },
          { yAxis: lcl, label: { formatter: `LCL\n${lcl}` }, lineStyle: { color: 'red', type: 'dashed' } }
        ]
      }
    }]
  }
})

// R chart option
const rChartOption = computed(() => {
  if (!xbarRData.value?.rData) return {}
  
  const ucl = xbarRData.value.rUcl
  const lcl = xbarRData.value.rLcl || 0
  const mean = xbarRData.value.rMean

  return {
    title: { text: 'R Chart (Range)', left: 'center' },
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', name: 'Subgroup', data: xbarRData.value.rData.map(d => d.subgroupIndex) },
    yAxis: { type: 'value', name: 'R' },
    series: [{
      type: 'line',
      data: xbarRData.value.rData.map(d => d.value),
      smooth: true,
      markLine: {
        silent: true,
        symbol: 'none',
        data: [
          { yAxis: ucl, label: { formatter: `UCL\n${ucl}` }, lineStyle: { color: 'red', type: 'dashed' } },
          { yAxis: mean, label: { formatter: `R̅\n${mean}` }, lineStyle: { color: 'green' } },
          { yAxis: lcl, label: { formatter: `LCL\n${lcl}` }, lineStyle: { color: 'red', type: 'dashed' } }
        ]
      }
    }]
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

header { text-align: center; margin-bottom: 20px; }
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

input, select {
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
  margin-bottom: 30px;
}

.chart { height: 350px; width: 100%; }

.legacy-section {
  margin-top: 40px;
  padding-top: 20px;
  border-top: 2px solid #ddd;
}

.legacy-section summary {
  cursor: pointer;
  font-weight: bold;
  color: #666;
}
</style>
