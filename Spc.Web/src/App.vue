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
        <label>Chart Type:</label>
        <select v-model="chartType">
          <option value="R">X-bar + R</option>
          <option value="S">X-bar + S</option>
        </select>
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
    </div>

    <!-- Data Source Selection -->
    <div class="data-source">
      <h3>📊 Data Source</h3>
      <div class="source-options">
        <label class="source-option">
          <input type="radio" v-model="dataSource" value="simulate" />
          <span>🎲 Simulate (Random Data)</span>
        </label>
        <label class="source-option">
          <input type="radio" v-model="dataSource" value="csv" />
          <span>📁 Upload CSV File</span>
        </label>
      </div>
      
      <!-- CSV Upload (shown when CSV is selected) -->
      <div v-if="dataSource === 'csv'" class="csv-upload">
        <input 
          type="file" 
          accept=".csv" 
          @change="handleFileSelect" 
          :disabled="loading"
        />
        <p class="csv-hint">CSV format: Value,Timestamp (header required)</p>
      </div>

      <!-- Generate Button -->
      <button @click="generateChart" :disabled="loading" class="generate-btn">
        {{ loading ? 'Processing...' : (dataSource === 'csv' ? '📥 Load CSV Data' : '🎲 Generate Chart') }}
      </button>

      <!-- Import Result -->
      <div v-if="importResult" class="import-result" :class="importResult.success ? 'success' : 'error'">
        {{ importResult.message }}
        <span v-if="importResult.recordsImported">
          - {{ importResult.recordsImported }} records, {{ importResult.subgroupsCreated }} subgroups
        </span>
      </div>
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
        <span class="label">x̅ (Mean):</span>
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

    <!-- R Chart (show when chartType = R) -->
    <template v-if="chartType === 'R'">
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
    </template>

    <!-- S Chart (show when chartType = S) -->
    <template v-if="chartType === 'S'">
      <div v-if="xbarRData" class="status" :class="xbarRData.sStatus.toLowerCase()">
        <span class="status-label">S Chart Status:</span>
        <span class="status-value">{{ xbarRData.sStatus }}</span>
        <span v-if="xbarRData.sRules?.length" class="rules">
          ({{ xbarRData.sRules.join(', ') }})
        </span>
      </div>

      <!-- S Chart Stats -->
      <div class="stats" v-if="xbarRData">
        <div class="stat">
          <span class="label">S̅ (Avg StdDev):</span>
          <span class="value">{{ xbarRData.sMean }}</span>
        </div>
        <div class="stat">
          <span class="label">UCL:</span>
          <span class="value">{{ xbarRData.sUcl }}</span>
        </div>
        <div class="stat">
          <span class="label">LCL:</span>
          <span class="value">{{ xbarRData.sLcl ?? 0 }}</span>
        </div>
      </div>

      <!-- S Chart -->
      <div class="chart-container">
        <v-chart class="chart" :option="sChartOption" autoresize />
      </div>
    </template>

    <!-- Data Table -->
    <div v-if="xbarRData" class="data-table-container">
      <h3>📋 Data Table</h3>
      <table class="data-table">
        <thead>
          <tr>
            <th>Subgroup</th>
            <th v-if="chartType === 'R'">Range (R)</th>
            <th v-if="chartType === 'S'">StdDev (S)</th>
            <th>Mean (x̅)</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="i in (xbarRData.xBarData?.length || 0)" :key="i">
            <td>{{ i }}</td>
            <td v-if="chartType === 'R'">{{ xbarRData.rData?.[i-1]?.value?.toFixed(4) }}</td>
            <td v-if="chartType === 'S'">{{ xbarRData.sData?.[i-1]?.value?.toFixed(4) }}</td>
            <td>{{ xbarRData.xBarData?.[i-1]?.value?.toFixed(4) }}</td>
          </tr>
        </tbody>
      </table>
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

// API base URL
const API_BASE = import.meta.env.VITE_API_URL || 'https://spc-api-ap7g.onrender.com'

// Legacy single value mode
const machineId = ref('M001')
const itemName = ref('Thickness')
const loading = ref(false)
const lastResult = ref(null)
const chartData = ref([])

// X-bar + R/S chart mode
const subgroupSize = ref(5)
const chartType = ref('R')
const xbarRData = ref(null)

// Data source selection
const dataSource = ref('simulate') // 'simulate' or 'csv'
const selectedFile = ref(null)
const importResult = ref(null)

const handleFileSelect = (event) => {
  selectedFile.value = event.target.files[0]
  importResult.value = null
}

// Unified generate function
const generateChart = async () => {
  if (dataSource.value === 'csv') {
    if (!selectedFile.value) {
      alert('Please select a CSV file first')
      return
    }
    await importCsv()
  } else {
    await simulateXBarR()
  }
}

const importCsv = async () => {
  if (!selectedFile.value) return
  
  loading.value = true
  importResult.value = null
  
  try {
    const formData = new FormData()
    formData.append('file', selectedFile.value)
    
    const res = await axios.post(`${API_BASE}/api/spc/import-csv`, formData, {
      params: { 
        machineId: machineId.value, 
        itemName: itemName.value,
        subgroupSize: subgroupSize.value
      },
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    
    importResult.value = res.data
    
    // Refresh chart data after import
    if (res.data.success) {
      await simulateXBarR()
    }
  } catch (e) {
    console.error(e)
    importResult.value = {
      success: false,
      message: 'Import failed: ' + e.message
    }
  } finally {
    loading.value = false
  }
}

// Simulate X-bar + R or X-bar + S chart
const simulateXBarR = async () => {
  loading.value = true
  try {
    // Call appropriate API based on chart type
    const endpoint = chartType.value === 'S' ? 'simulate-xbar-s' : 'simulate-xbar-r'
    const res = await axios.get(`${API_BASE}/api/spc/${endpoint}`, {
      params: { 
        machineId: machineId.value, 
        itemName: itemName.value,
        subgroupSize: subgroupSize.value
      }
    })
    xbarRData.value = res.data
    
    // Fetch full chart data - reduced to 15 subgroups for cleaner display
    const dataRes = await axios.get(`${API_BASE}/api/spc/xbar-r-data`, {
      params: { 
        machineId: machineId.value, 
        itemName: itemName.value,
        subgroupSize: subgroupSize.value,
        numSubgroups: 15,
        chartType: chartType.value
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
    title: { text: 'X-bar Chart (Subgroup Means)', left: 'center', top: 10 },
    tooltip: { trigger: 'axis' },
    grid: { top: 90, bottom: 60, left: 70, right: 50 },
    xAxis: { 
      type: 'category', 
      name: 'Subgroup', 
      nameLocation: 'middle',
      nameGap: 35,
      data: xbarRData.value.xBarData.map(d => d.subgroupIndex),
      axisLabel: { interval: 0, rotate: 0, fontSize: 11 }
    },
    yAxis: { 
      type: 'value', 
      name: 'x̅', 
      nameLocation: 'middle', 
      nameGap: 50,
      min: (value) => Math.floor(value.min - (value.max - value.min) * 0.1),
      max: (value) => Math.ceil(value.max + (value.max - value.min) * 0.1)
    },
    series: [{
      type: 'line',
      data: data.map(d => d.value[1]),
      smooth: true,
      lineStyle: { width: 2 },
      markLine: {
        silent: true,
        symbol: 'none',
        animation: false,
        data: [
          { yAxis: ucl, label: { formatter: 'UCL', position: 'insideEndTop', color: 'red' }, lineStyle: { color: 'red', type: 'dashed', width: 1 } },
          { yAxis: mean, label: { formatter: 'CL', position: 'middle', color: 'green' }, lineStyle: { color: 'green', type: 'solid', width: 1 } },
          { yAxis: lcl, label: { formatter: 'LCL', position: 'insideEndBottom', color: 'red' }, lineStyle: { color: 'red', type: 'dashed', width: 1 } }
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
    title: { text: 'R Chart (Range)', left: 'center', top: 10 },
    tooltip: { trigger: 'axis' },
    grid: { top: 90, bottom: 60, left: 70, right: 50 },
    xAxis: { 
      type: 'category', 
      name: 'Subgroup', 
      nameLocation: 'middle',
      nameGap: 35,
      data: xbarRData.value.rData.map(d => d.subgroupIndex),
      axisLabel: { interval: 0, fontSize: 11 }
    },
    yAxis: { 
      type: 'value', 
      name: 'R', 
      nameLocation: 'middle', 
      nameGap: 50,
      min: (value) => Math.max(0, Math.floor(value.min - (value.max - value.min) * 0.1)),
      max: (value) => Math.ceil(value.max + (value.max - value.min) * 0.1)
    },
    series: [{
      type: 'line',
      data: xbarRData.value.rData.map(d => d.value),
      smooth: true,
      lineStyle: { width: 2 },
      markLine: {
        silent: true,
        symbol: 'none',
        animation: false,
        data: [
          { yAxis: ucl, label: { formatter: 'UCL', position: 'insideEndTop', color: 'red' }, lineStyle: { color: 'red', type: 'dashed', width: 1 } },
          { yAxis: mean, label: { formatter: 'R̅', position: 'middle', color: 'green' }, lineStyle: { color: 'green', type: 'solid', width: 1 } },
          { yAxis: lcl, label: { formatter: 'LCL', position: 'insideEndBottom', color: 'red' }, lineStyle: { color: 'red', type: 'dashed', width: 1 } }
        ]
      }
    }]
  }
})

// S chart option
const sChartOption = computed(() => {
  if (!xbarRData.value?.sData) return {}
  
  const ucl = xbarRData.value.sUcl
  const lcl = xbarRData.value.sLcl || 0
  const mean = xbarRData.value.sMean

  return {
    title: { text: 'S Chart (Standard Deviation)', left: 'center', top: 10 },
    tooltip: { trigger: 'axis' },
    grid: { top: 90, bottom: 60, left: 70, right: 50 },
    xAxis: { 
      type: 'category', 
      name: 'Subgroup', 
      nameLocation: 'middle',
      nameGap: 35,
      data: xbarRData.value.sData.map(d => d.subgroupIndex),
      axisLabel: { interval: 0, fontSize: 11 }
    },
    yAxis: { 
      type: 'value', 
      name: 'S', 
      nameLocation: 'middle', 
      nameGap: 50,
      min: (value) => Math.max(0, Math.floor(value.min - (value.max - value.min) * 0.1)),
      max: (value) => Math.ceil(value.max + (value.max - value.min) * 0.1)
    },
    series: [{
      type: 'line',
      data: xbarRData.value.sData.map(d => d.value),
      smooth: true,
      lineStyle: { width: 2 },
      markLine: {
        silent: true,
        symbol: 'none',
        animation: false,
        data: [
          { yAxis: ucl, label: { formatter: 'UCL', position: 'insideEndTop', color: 'red' }, lineStyle: { color: 'red', type: 'dashed', width: 1 } },
          { yAxis: mean, label: { formatter: 'S̅', position: 'middle', color: 'green' }, lineStyle: { color: 'green', type: 'solid', width: 1 } },
          { yAxis: lcl, label: { formatter: 'LCL', position: 'insideEndBottom', color: 'red' }, lineStyle: { color: 'red', type: 'dashed', width: 1 } }
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

.chart { height: 450px; width: 100%; }

.data-table-container {
  background: white;
  border-radius: 8px;
  padding: 20px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  margin-top: 20px;
}

.data-table-container h3 {
  margin-bottom: 15px;
  color: #2c3e50;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

.data-table th,
.data-table td {
  padding: 10px;
  text-align: center;
  border-bottom: 1px solid #eee;
}

.data-table th {
  background: #f8f9fa;
  font-weight: 600;
  color: #495057;
}

.data-table tbody tr:hover {
  background: #f8f9fa;
}

.csv-import {
  background: white;
  border-radius: 8px;
  padding: 20px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  margin-bottom: 30px;
}

.csv-import h3 {
  margin-bottom: 15px;
  color: #2c3e50;
}

.data-source {
  background: white;
  border-radius: 8px;
  padding: 20px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  margin-bottom: 30px;
}

.data-source h3 {
  margin-bottom: 15px;
  color: #2c3e50;
}

.source-options {
  display: flex;
  gap: 20px;
  margin-bottom: 15px;
}

.source-option {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  padding: 10px 15px;
  border: 2px solid #ddd;
  border-radius: 8px;
  transition: all 0.2s;
}

.source-option:has(input:checked) {
  border-color: #3498db;
  background: #f0f8ff;
}

.source-option input {
  margin: 0;
}

.csv-upload {
  margin-bottom: 15px;
}

.csv-upload input[type="file"] {
  padding: 8px;
  border: 1px solid #ddd;
  border-radius: 4px;
  background: white;
}

.generate-btn {
  padding: 12px 30px;
  background: #27ae60;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 16px;
  font-weight: bold;
}

.generate-btn:hover { background: #219a52; }
.generate-btn:disabled { background: #95a5a6; }

.csv-hint {
  margin-top: 10px;
  font-size: 12px;
  color: #666;
}

.import-result {
  margin-top: 15px;
  padding: 12px;
  border-radius: 4px;
  font-weight: bold;
}

.import-result.success {
  background: #d4edda;
  color: #155724;
}

.import-result.error {
  background: #f8d7da;
  color: #721c24;
}

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
