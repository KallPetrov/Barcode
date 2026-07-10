package com.barcodeplatform.pda.ui

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Build
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.barcodeplatform.pda.CalacApp
import com.barcodeplatform.pda.data.api.PickingOrderDto
import com.barcodeplatform.pda.data.api.PickingStockLineDto
import com.barcodeplatform.pda.data.api.UpdatePickingLineRequest
import com.barcodeplatform.pda.databinding.ActivityPickingDetailBinding
import com.barcodeplatform.pda.scanner.DataWedgeReceiver
import kotlinx.coroutines.launch

class PickingDetailActivity : AppCompatActivity() {
    private lateinit var binding: ActivityPickingDetailBinding
    private var orderId: String? = null
    private var currentOrder: PickingOrderDto? = null
    private var voiceCommandService: VoiceCommandService? = null
    private val adapter = StockLineAdapter { line ->
        // Logic for manual pick if needed
    }

    private val scanReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            val barcode = intent?.getStringExtra(DataWedgeReceiver.EXTRA_BARCODE) ?: return
            onBarcodeScanned(barcode)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityPickingDetailBinding.inflate(layoutInflater)
        setContentView(binding.root)

        orderId = intent.getStringExtra("ORDER_ID")
        binding.rvStockLines.layoutManager = LinearLayoutManager(this)
        binding.rvStockLines.adapter = adapter

        binding.btnStart.setOnClickListener { startPicking() }
        binding.btnComplete.setOnClickListener { completePicking() }
        binding.btnVoiceCommandDetail.setOnClickListener { startVoiceRecognition() }

        loadOrder()
    }

    private fun startVoiceRecognition() {
        if (voiceCommandService == null) {
            voiceCommandService = VoiceCommandService(this) { command ->
                handleVoiceCommand(command)
            }
        }
        voiceCommandService?.startListening()
        Toast.makeText(this, "Слушам...", Toast.LENGTH_SHORT).show()
    }

    private fun handleVoiceCommand(command: String) {
        val voice = (application as CalacApp).voiceService
        when {
            command.contains("къде съм") || command.contains("задача") || command.contains("инструкция") -> {
                val nextLine = currentOrder?.lines?.flatMap { it.stockLines }?.firstOrNull { it.pickedAt.isNullOrEmpty() }
                if (nextLine != null) {
                    voice.speak("Намирате се в процес на picking. Отидете на локация ${nextLine.locationName} за артикул ${nextLine.itemName}")
                } else {
                    voice.speak("Всички артикули са събрани. Моля, завършете поръчката.")
                }
            }
            command.contains("завърши") -> {
                voice.speak("Опит за завършване на поръчката")
                completePicking()
            }
            else -> {
                voice.speak("Непозната команда")
            }
        }
    }

    private fun loadOrder() {
        lifecycleScope.launch {
            val repo = (application as CalacApp).repository
            orderId?.let { id ->
                repo.getPickingOrder(id).onSuccess { order ->
                    updateUI(order)
                }
            }
        }
    }

    private fun updateUI(order: PickingOrderDto) {
        currentOrder = order
        binding.txtOrderName.text = order.name
        binding.txtStatus.text = "Статус: ${order.status}"

        val allStockLines = order.lines.flatMap { it.stockLines }
        adapter.submitList(allStockLines)

        binding.btnStart.visibility = if (order.status == "Draft") View.VISIBLE else View.GONE
        binding.btnComplete.visibility = if (order.status == "InProgress") View.VISIBLE else View.GONE

        // Voice Guidance Logic will be added in the next step
        announceNextStep(order)
    }

    private fun startPicking() {
        lifecycleScope.launch {
            val repo = (application as CalacApp).repository
            orderId?.let { id ->
                repo.startPickingOrder(id).onSuccess { updateUI(it) }
            }
        }
    }

    private fun completePicking() {
        lifecycleScope.launch {
            val repo = (application as CalacApp).repository
            orderId?.let { id ->
                repo.completePickingOrder(id).onSuccess {
                    updateUI(it)
                    Toast.makeText(this@PickingDetailActivity, "Поръчката е завършена!", Toast.LENGTH_SHORT).show()
                    finish()
                }
            }
        }
    }

    private fun onBarcodeScanned(barcode: String) {
        val nextLine = currentOrder?.lines?.flatMap { it.stockLines }?.firstOrNull { it.pickedAt.isNullOrEmpty() }

        if (nextLine != null) {
            // Simulated validation: in a real app, we verify the barcode matches the item
            val voice = (application as CalacApp).voiceService
            lifecycleScope.launch {
                val repo = (application as CalacApp).repository
                val request = UpdatePickingLineRequest(
                    lineId = nextLine.id,
                    pickedQuantity = nextLine.quantity
                )
                repo.updatePickingLine(currentOrder!!.id, request).onSuccess {
                    voice.speak("Вземете ${nextLine.quantity} броя")
                    updateUI(it)
                }.onFailure {
                    voice.speak("Грешка при отчитане")
                }
            }
        }
    }

    private fun announceNextStep(order: PickingOrderDto) {
        if (order.status != "InProgress") return
        val nextLine = order.lines.flatMap { it.stockLines }.firstOrNull { it.pickedAt.isNullOrEmpty() }
        if (nextLine != null) {
            val voice = (application as CalacApp).voiceService
            voice.speak("Отидете на локация ${nextLine.locationName} за артикул ${nextLine.itemName}")
        } else if (order.status == "InProgress") {
            (application as CalacApp).voiceService.speak("Всички артикули са събрани. Моля, завършете поръчката.")
        }
    }

    override fun onResume() {
        super.onResume()
        registerReceiver(scanReceiver, IntentFilter(DataWedgeReceiver.ACTION_SCAN))
    }

    override fun onPause() {
        unregisterReceiver(scanReceiver)
        voiceCommandService?.stopListening()
        super.onPause()
    }

    override fun onDestroy() {
        voiceCommandService?.destroy()
        super.onDestroy()
    }

    class StockLineAdapter(private val onClick: (PickingStockLineDto) -> Unit) : RecyclerView.Adapter<StockLineAdapter.ViewHolder>() {
        private var list = listOf<PickingStockLineDto>()
        fun submitList(newList: List<PickingStockLineDto>) {
            list = newList
            notifyDataSetChanged()
        }
        class ViewHolder(view: View) : RecyclerView.ViewHolder(view) {
            val info: TextView = view.findViewById(android.R.id.text1)
            val subInfo: TextView = view.findViewById(android.R.id.text2)
        }
        override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
            val view = LayoutInflater.from(parent.context).inflate(android.R.layout.simple_list_item_2, parent, false)
            return ViewHolder(view)
        }
        override fun onBindViewHolder(holder: ViewHolder, position: Int) {
            val line = list[position]
            holder.info.text = "${line.itemName} - ${line.quantity} бр."
            holder.subInfo.text = "Локация: ${line.locationName} | ${if (line.pickedAt.isNullOrEmpty()) "ЧАКА" else "ВЗЕТО"}"
            holder.itemView.setOnClickListener { onClick(line) }
        }
        override fun getItemCount() = list.size
    }
}
