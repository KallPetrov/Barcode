package com.barcodeplatform.pda.ui

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Build
import android.os.Bundle
import android.os.VibrationEffect
import android.os.Vibrator
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import android.Manifest
import android.content.pm.PackageManager
import com.barcodeplatform.pda.CalacApp
import com.barcodeplatform.pda.databinding.ActivityMainBinding
import com.barcodeplatform.pda.scanner.DataWedgeReceiver
import com.barcodeplatform.pda.voice.VoiceCommandService
import com.google.gson.JsonObject
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity() {
    private lateinit var binding: ActivityMainBinding
    private lateinit var repo: com.barcodeplatform.pda.data.repository.PlatformRepository
    private var voiceCommandService: VoiceCommandService? = null

    private val scanReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            val barcode = intent?.getStringExtra(DataWedgeReceiver.EXTRA_BARCODE) ?: return
            val symbology = intent.getStringExtra(DataWedgeReceiver.EXTRA_SYMBOLOGY) ?: "Unknown"
            onBarcodeScanned(barcode, symbology)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val repo = (application as CalacApp).repository
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.txtUser.text = repo.userName ?: "Оператор"
        binding.txtDeviceId.text = "ID: ${repo.hardwareId}"
        binding.txtSyncStatus.text = "Статус: Готово за синхронизация"

        val voicePrefs = getSharedPreferences("voice_prefs", Context.MODE_PRIVATE)
        binding.cbVoiceEnabled.isChecked = voicePrefs.getBoolean("voice_enabled", true)
        binding.cbVoiceEnabled.setOnCheckedChangeListener { _, isChecked ->
            voicePrefs.edit().putBoolean("voice_enabled", isChecked).apply()
        }

        binding.btnPicking.setOnClickListener {
            startActivity(Intent(this, PickingListActivity::class.java))
        }
        binding.btnSync.setOnClickListener { syncNow() }
        binding.btnLogout.setOnClickListener { logout() }

        binding.btnVoiceCommand.setOnClickListener {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(this, arrayOf(Manifest.permission.RECORD_AUDIO), 101)
            } else {
                startVoiceRecognition()
            }
        }

        refreshPendingCount()
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
        when {
            command.contains("синхронизирай") || command.contains("изпрати") -> {
                (application as CalacApp).voiceService.speak("Стартирам синхронизация")
                syncNow()
            }
            command.contains("изход") || command.contains("отписване") -> {
                logout()
            }
            command.contains("повтори") -> {
                val lastScan = binding.txtLastScan.text.toString()
                if (lastScan != "—") {
                    (application as CalacApp).voiceService.speak("Последно сканиран: $lastScan")
                } else {
                    (application as CalacApp).voiceService.speak("Няма последно сканирани данни")
                }
            }
            else -> {
                (application as CalacApp).voiceService.speak("Непозната команда: $command")
            }
        }
    }

    private fun logout() {
        (application as CalacApp).repository.logout()
        startActivity(Intent(this, LoginActivity::class.java))
        finish()
    }

    override fun onResume() {
        super.onResume()
        val filter = IntentFilter(DataWedgeReceiver.ACTION_SCAN)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            registerReceiver(scanReceiver, filter, RECEIVER_NOT_EXPORTED)
        } else {
            @Suppress("UnspecifiedRegisterReceiverFlag")
            registerReceiver(scanReceiver, filter)
        }
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

    private fun onBarcodeScanned(barcode: String, symbology: String) {
        vibrateSuccess()
        (application as CalacApp).voiceService.speak("Артикулът е разпознат")
        binding.txtLastScan.text = "$barcode ($symbology)"
        binding.txtSyncStatus.text = "Статус: Сканиран баркод, записване в локалната опашка"
        lifecycleScope.launch {
            val payload = JsonObject().apply {
                addProperty("barcode", barcode)
                addProperty("symbology", symbology)
            }.toString()
            repo.queueOperation("BARCODE_SCAN", payload)
            refreshPendingCount()
            syncNow(showToast = false)
        }
    }

    private fun syncNow(showToast: Boolean = true) {
        lifecycleScope.launch {
            repo.syncPending()
                .onSuccess { (successful, pending) ->
                    refreshPendingCount()
                    binding.txtSyncStatus.text = if (pending == 0) {
                        "Статус: Синхронизация успешно завършена ($successful)"
                    } else {
                        "Статус: Изпратени $successful, чакащи $pending"
                    }
                    if (showToast) {
                        Toast.makeText(
                            this@MainActivity,
                            if (pending == 0) "Синхронизирано ($successful)" else "Изпратени: $successful, чакащи: $pending",
                            Toast.LENGTH_SHORT
                        ).show()
                    }
                }
                .onFailure {
                    (application as CalacApp).voiceService.speak("Грешка при синхронизация")
                    binding.txtSyncStatus.text = "Статус: Офлайн — данните са запазени локално"
                    if (showToast) {
                        Toast.makeText(this@MainActivity, "Offline — данните са запазени локално", Toast.LENGTH_SHORT).show()
                    }
                    refreshPendingCount()
                }
        }
    }

    private fun refreshPendingCount() {
        lifecycleScope.launch {
            binding.txtPending.text = "Чакащи операции: ${repo.pendingCount()}"
        }
    }

    private fun vibrateSuccess() {
        val vibrator = getSystemService(VIBRATOR_SERVICE) as Vibrator
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            vibrator.vibrate(VibrationEffect.createOneShot(80, VibrationEffect.DEFAULT_AMPLITUDE))
        } else {
            @Suppress("DEPRECATION")
            vibrator.vibrate(80)
        }
    }
}
