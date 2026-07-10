package com.barcodeplatform.pda.ui

import android.content.Intent
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
import com.barcodeplatform.pda.databinding.ActivityPickingListBinding
import kotlinx.coroutines.launch

class PickingListActivity : AppCompatActivity() {
    private lateinit var binding: ActivityPickingListBinding
    private val adapter = PickingAdapter { order ->
        val intent = Intent(this, PickingDetailActivity::class.java).apply {
            putExtra("ORDER_ID", order.id)
        }
        startActivity(intent)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityPickingListBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.rvOrders.layoutManager = LinearLayoutManager(this)
        binding.rvOrders.adapter = adapter

        refreshOrders()
    }

    private fun refreshOrders() {
        lifecycleScope.launch {
            val repo = (application as CalacApp).repository
            repo.getPickingOrders()
                .onSuccess { orders ->
                    adapter.submitList(orders)
                    if (orders.isEmpty()) {
                        Toast.makeText(this@PickingListActivity, "Няма чакащи поръчки", Toast.LENGTH_SHORT).show()
                    }
                }
                .onFailure {
                    Toast.makeText(this@PickingListActivity, "Грешка при извличане на поръчки", Toast.LENGTH_SHORT).show()
                }
        }
    }

    class PickingAdapter(private val onClick: (PickingOrderDto) -> Unit) : RecyclerView.Adapter<PickingAdapter.ViewHolder>() {
        private var list = listOf<PickingOrderDto>()

        fun submitList(newList: List<PickingOrderDto>) {
            list = newList
            notifyDataSetChanged()
        }

        class ViewHolder(view: View) : RecyclerView.ViewHolder(view) {
            val name: TextView = view.findViewById(android.R.id.text1)
            val status: TextView = view.findViewById(android.R.id.text2)
        }

        override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
            val view = LayoutInflater.from(parent.context).inflate(android.R.layout.simple_list_item_2, parent, false)
            return ViewHolder(view)
        }

        override fun onBindViewHolder(holder: ViewHolder, position: Int) {
            val order = list[position]
            holder.name.text = order.name
            holder.status.text = "Статус: ${order.status} | Линии: ${order.lines.size}"
            holder.itemView.setOnClickListener { onClick(order) }
        }

        override fun getItemCount() = list.size
    }
}
