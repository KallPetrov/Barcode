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
import com.barcodeplatform.pda.BarcodePlatformApp
import com.barcodeplatform.pda.databinding.ActivityMainBinding
import com.barcodeplatform.pda.scanner.DataWedgeReceiver
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity() {
    private lateinit var binding: ActivityMainBinding
    private lateinit var repo: com.barcodeplatform.pda.data.repository.PlatformRepository

    private val scanReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            val barcode = intent?.getStringExtra(DataWedgeReceiver.EXTRA_BARCODE) ?: return
            onBarcodeScanned(barcode)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        repo = (application as BarcodePlatformApp).repository
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.txtUser.text = repo.userName ?: "Оператор"
        binding.txtDeviceId.text = "ID: ${repo.hardwareId}"
        binding.txtSyncStatus.text = "Статус: Готово за синхронизация"

        binding.btnSync.setOnClickListener { syncNow() }
        binding.btnLogout.setOnClickListener {
            repo.logout()
            startActivity(Intent(this, LoginActivity::class.java))
            finish()
        }

        refreshPendingCount()
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
        super.onPause()
    }

    private fun onBarcodeScanned(barcode: String) {
        vibrateSuccess()
        binding.txtLastScan.text = barcode
        binding.txtSyncStatus.text = "Статус: Сканиран баркод, записване в локалната опашка"
        lifecycleScope.launch {
            repo.queueOperation("BARCODE_SCAN", """{"barcode":"$barcode"}""")
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
