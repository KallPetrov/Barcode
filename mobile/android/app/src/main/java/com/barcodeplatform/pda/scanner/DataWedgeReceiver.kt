package com.barcodeplatform.pda.scanner

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

class DataWedgeReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context?, intent: Intent?) {
        if (intent == null) return

        val barcode = intent.getStringExtra("com.symbol.datawedge.data_string") // Zebra
            ?: intent.getStringExtra("com.honeywell.decode.intent.extras.DATA") // Honeywell
            ?: intent.getStringExtra("com.datalogic.decode.intent.extras.barcode_string") // Datalogic
            ?: intent.getStringExtra("data")
            ?: return

        val scanIntent = Intent(ACTION_SCAN).apply {
            putExtra(EXTRA_BARCODE, barcode)
            setPackage(context?.packageName)
        }
        context?.sendBroadcast(scanIntent)
    }

    companion object {
        const val ACTION_SCAN = "com.barcodeplatform.pda.SCAN"
        const val EXTRA_BARCODE = "barcode"
    }
}
